using DocumentFormat.OpenXml.Drawing.Charts;
using GownApi.Model;
using GownApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;



namespace GownApi.Endpoints
{
    public static class PaymentEndpoints
    {
        record PaymentRequest(int PayAmount, int OrderId);

        public static void MapPaymentEndpoints(this WebApplication app)
        {
            app.MapPost("/api/payment/create-payment", async (
                PaymentRequest request,
                GownDb db,
                IHttpClientFactory httpClientFactory,
                IQueueJobPublisher publisher,
                IOptions<PaystationOptions> paystationOptions,
                ILogger<Program> logger,
                HttpContext httpContext,
                CancellationToken ct
                ) =>
            {
                if (request.PayAmount <= 0 || request.OrderId <= 0)
                {
                    logger.LogWarning("Invalid create-payment request: Amount={PayAmount}, OrderId={OrderId}",
                        request.PayAmount, request.OrderId);
                    return Results.BadRequest("Invalid payment request");
                }

                var settings = paystationOptions.Value; 
                var paystationUrl = settings.BaseUrl;
                var paystationId = settings.PaystationId;
                var gatewayId = settings.GatewayId;
                var nzTz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");

                if (string.IsNullOrWhiteSpace(paystationId) || string.IsNullOrWhiteSpace(gatewayId) || string.IsNullOrWhiteSpace(paystationUrl))
                {
                    logger.LogError("Paystation config missing. PaystationId/GatewayId/PaystationUrl not set.");
                    return Results.Problem("Paystation config missing.", statusCode: 500);
                }

                var orderId = request.OrderId;
                var merchantReference = $"REF{orderId}";
                var merchantSession = $"REF{orderId}-{Guid.NewGuid():N}";
                httpContext.Items["MerchantReference"] = merchantReference;

                var order = await db.orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
                if (order == null)
                {
                    logger.LogWarning("Create-payment failed: order not found. OrderId={OrderId}", request.OrderId);
                    return Results.NotFound("Order not found.");
                }

                if (order.Paid == true)
                {
                    logger.LogWarning("Create-payment failed: order already paid. OrderId={OrderId}", request.OrderId);
                    return Results.BadRequest("Order is already paid.");
                }

                var expectedAmountCents = (int)Math.Round(order.OrderAmount * 100m, MidpointRounding.AwayFromZero);

                if (request.PayAmount != expectedAmountCents)
                {
                    logger.LogWarning("Create-payment failed: amount mismatch. OrderId={OrderId}, RequestAmount={RequestAmount}, ExpectedAmount={ExpectedAmount}",
                        request.OrderId, request.PayAmount, expectedAmountCents);
                    return Results.BadRequest("Payment amount mismatch.");
                }

                using (Serilog.Context.LogContext.PushProperty("MerchantReference", merchantReference))
                {
                    logger.LogInformation("Paystation init start. PaystationId={PaystationId}, GatewayId={GatewayId}",
                        paystationId, gatewayId);

                    try
                    {
                        var httpClient = httpClientFactory.CreateClient("Paystation");

                        var values = new Dictionary<string, string>
                        {
                            { "paystation", "_empty" },
                            { "pstn_pi", paystationId },
                            { "pstn_gi", gatewayId },
                            { "pstn_am", request.PayAmount.ToString(CultureInfo.InvariantCulture) },
                            { "pstn_ms", merchantSession },
                            { "pstn_nr", "t" },
                            { "pstn_mr", merchantReference }
                        };

                        using var response = await httpClient.PostAsync(
                            paystationUrl,
                            new FormUrlEncodedContent(values),
                            ct
                        );

                        var responseString = await response.Content.ReadAsStringAsync(ct);
                        var redirectUrl = ExtractRedirectUrl(responseString);

                        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(redirectUrl))
                        {
                            logger.LogWarning("Paystation init failed. StatusCode={StatusCode}, RedirectUrlEmpty={RedirectEmpty}, BodySnippet={BodySnippet}",
                                (int)response.StatusCode,
                                redirectUrl,
                                SafeSnippet(responseString, 6000));

                            try
                            {
                                await publisher.EnqueueEmailJobAsync(new EmailJob(
                                Type: "PaymentAlertInitFailed",
                                OrderId: null,
                                ReferenceNo: merchantReference,
                                TxnId: $"http:{(int)response.StatusCode}",
                                OccurredAt: TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, nzTz),
                                EmailQueueItemId: null
                                //OccurredAt: DateTimeOffset.UtcNow
                                 ));
                            }
                            catch (Exception enqueueEx)
                            {
                                logger.LogError(enqueueEx, "Failed to enqueue alert email job.");
                            }

                            return Results.Problem("Failed to initiate payment.", statusCode: 502);
                        }

                        logger.LogInformation("Paystation init success. StatusCode={StatusCode}, RedirectUrl={RedirectUrl}, BodySnippet={BodySnippet}",
                              (int)response.StatusCode,
                              redirectUrl,
                              SafeSnippet(responseString, 6000));

                        return Results.Ok(new { redirectUrl });
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {

                        logger.LogInformation("Create-payment canceled by client. OrderId={OrderId}, MerchantReference={MerchantReference}",
                            orderId, merchantReference);
                        return Results.Problem("Request canceled.", statusCode: 499);
                    }
                    catch (OperationCanceledException ex)
                    {
                        logger.LogError(ex, "Paystation init timed out or was canceled upstream.");

                        try
                        {
                            await publisher.EnqueueEmailJobAsync(new EmailJob(
                                Type: "PaymentAlertInitTimeout",
                                OrderId: null,
                                ReferenceNo: merchantReference,
                                TxnId: "upstream-timeout",
                                OccurredAt: TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, nzTz),
                                EmailQueueItemId: null
                            //OccurredAt: DateTimeOffset.UtcNow
                            ));
                        }
                        catch (Exception enqueueEx)
                        {
                            logger.LogError(enqueueEx, "Failed to enqueue alert email job.");
                        }

                        return Results.Problem("Payment service timed out.", statusCode: 504);
                    }
                    catch (HttpRequestException ex)
                    {
                        logger.LogError(ex, "Paystation init HTTP exception.");

                        try
                        {
                            await publisher.EnqueueEmailJobAsync(new EmailJob(
                                Type: "PaymentAlertInitException",
                                OrderId: null,
                                ReferenceNo: merchantReference,
                                TxnId: ex.GetType().Name,
                                OccurredAt: TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, nzTz),
                                EmailQueueItemId: null
                                //OccurredAt: DateTimeOffset.UtcNow
                                ));  
                        }
                        catch (Exception enqueueEx)
                        {
                            logger.LogError(enqueueEx, "Failed to enqueue alert email job.");
                        }
                        return Results.Problem("Failed to initiate payment.", statusCode: 502);
                    }
                }
            });

            app.MapPost("/notify", async (
                HttpRequest req,
                IConfiguration config,
                GownDb db,
                IQueueJobPublisher publisher,
                HttpContext httpContext,
                ILogger<Program> logger) =>
            {
                req.EnableBuffering();

                string raw;
                var nzTz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
                using (var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true))
                {
                    raw = await reader.ReadToEndAsync();
                    req.Body.Position = 0;
                }

                logger.LogInformation("Paystation notify raw XML. Length={Len}, BodySnippet={BodySnippet}",
                    raw?.Length ?? 0,
                    SafeSnippet(raw, 6000));

                XDocument doc;
                try
                {
                    doc = XDocument.Parse(raw);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Paystation notify invalid XML. ContentType={ContentType}, RemoteIP={RemoteIP}, BodySnippet={BodySnippet}",
                        req.ContentType,
                        req.HttpContext.Connection.RemoteIpAddress?.ToString(),
                        SafeSnippet(raw, 600));

                    logger.LogInformation("Paystation notify response: {Resp}", "INVALID_XML");
                    return Results.Ok("INVALID_XML");
                }

                string? Get(string n) => doc.Root?.Element(n)?.Value;

                int ec = int.TryParse(Get("ec"), out var ecVal) ? ecVal : -1;
                var em = Get("em");

                var txnId = Get("PaystationTransactionID")
                            ?? Get("TransactionID")
                            ?? Get("ti");

                var merchantSession = Get("MerchantSession")?.Trim();
                var merchantReference = Get("MerchantReference")?.Trim();
                var requestTime = Get("TransactionTime") ?? Get("PaymentRequestTime");
                var receiptTime = Get("DigitalOrderTime") ?? Get("DigitalReceiptTime");
                var purchaseAmountCents = int.TryParse(Get("PurchaseAmount"), out var cents) ? cents : 0;
                var purchaseAmount = purchaseAmountCents / 100m;
                var receiptNumber = Get("ReturnReceiptNumber");
                httpContext.Items["MerchantReference"] = merchantReference;

                using (Serilog.Context.LogContext.PushProperty("MerchantReference", merchantReference ?? ""))
                using (Serilog.Context.LogContext.PushProperty("PaystationTxnId", txnId ?? ""))
                using (Serilog.Context.LogContext.PushProperty("PaystationEC", ec))
                using (Serilog.Context.LogContext.PushProperty("AmountCents", purchaseAmountCents))
                using (Serilog.Context.LogContext.PushProperty("ReceiptNo", receiptNumber ?? ""))
                {
                    logger.LogInformation("Paystation notify received. ec={EC}, em={EM}, requestTime={RequestTime}, receiptTime={ReceiptTime},amountcents={Amount}, receipt={Receipt}",
                        ec, em, requestTime, receiptTime, purchaseAmountCents, receiptNumber);

                    if (string.IsNullOrWhiteSpace(merchantReference))
                    {
                        logger.LogWarning("Paystation notify missing MerchantReference. BodySnippet={BodySnippet}", SafeSnippet(raw, 600));
                        logger.LogInformation("Paystation notify response: {Resp}", "NO_MERCHANT_REFERENCE");
                        return Results.Ok("NO_MERCHANT_REFERENCE");
                    }

                    if (!TryParseOrderIdFromMerchantReference(merchantReference, out var orderId))
                    {
                        logger.LogWarning("Invalid MerchantReference format: {MerchantReference}", merchantReference);
                        logger.LogInformation("Paystation notify response: {Resp}", "INVALID_MERCHANT_REFERENCE");
                        return Results.Ok("INVALID_MERCHANT_REFERENCE");
                    }

                    Orders? order;
                    try
                    {
                        order = await db.orders
                            .FirstOrDefaultAsync(o => o.Id == orderId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to load order by MerchantReference={MerchantReference}", merchantReference);
                        logger.LogInformation("Paystation notify response: {Resp}", "DB_READ_FAILED");
                        return Results.Ok("DB_READ_FAILED");
                    }

                    if (order == null)
                    {
                        logger.LogWarning("No order found by ReferenceNo(MerchantReference)={MerchantReference}", merchantReference);
                        logger.LogInformation("Paystation notify response: {Resp}", "NO_ORDER_BY_REFERENCE");
                        return Results.Ok("NO_ORDER_BY_REFERENCE");
                    }

                   
                    order.PaymentEc = ec;
                    order.PaymentEm = em;
                    var wasPaid = order.Paid == true;
                    string odertype; 

                    order.Paid = (ec == 0);

                        if (ec == 0)
                        {
                            order.AmountPaid = purchaseAmount;
                            //Original Paystation payment transaction id for future refunds
                            order.PaymentTxnId = txnId;

                            if (!string.IsNullOrWhiteSpace(receiptTime) &&
                                DateTime.TryParseExact(
                                    receiptTime,
                                    "yyyy-MM-dd HH:mm:ss",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out var receiptDt))
                            {
                                order.OrderDate = DateOnly.FromDateTime(receiptDt);

                                logger.LogInformation(
                                    "Parsed receiptTime successfully. ReceiptTime={ReceiptTime}, OrderDate={OrderDate}",
                                    receiptTime,
                                    order.OrderDate);
                            }
                            else
                            {
                                logger.LogWarning(
                                    "Failed to parse receiptTime from Paystation. ReceiptTime={ReceiptTime}",
                                    receiptTime);
                            }
                        }
                        else
                        {
                            order.AmountPaid = 0;
                        }

                        try
                        {
                            await db.SaveChangesAsync();

                            // confirm saved
                            logger.LogInformation("Order payment saved. Paid={Paid}, AmountPaidCents={AmountPaid}, PaymentTxnId={PaymentTxnId}",
                                order.Paid, order.AmountPaid, order.PaymentTxnId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to save payment status to DB.");
                            logger.LogInformation("Paystation notify response: {Resp}", "DB_SAVE_FAILED");
                            return Results.Ok("DB_SAVE_FAILED");
                        }

                        if (ec != 0)
                        {
                            logger.LogWarning("Payment failed (ec!=0). Updated order as unpaid. ec={EC}, em={EM}", ec, em);
                            logger.LogInformation("Paystation notify response: {Resp}", "PAYMENT_FAILED_UPDATED");
                            return Results.Ok("PAYMENT_FAILED_UPDATED");
                        }

                        if (wasPaid)
                        {
                            logger.LogInformation("Duplicate successful notify ignored for email enqueue. OrderId={OrderId}", order.Id);
                            logger.LogInformation("Paystation notify response: {Resp}", "OK_ALREADY_PAID");
                            return Results.Ok("OK_ALREADY_PAID");
                        }

                        if (order.OrderType == "1")
                        {
                        odertype = "HirePaymentCompleted";
                    }else if (order.OrderType == "2")
                        {
                            odertype = "BuyPaymentCompleted";
                        }else
                        {
                        odertype = "CasualHirePaymentCompleted";
                    }

                    try
                        {
                            await publisher.EnqueueEmailJobAsync(new EmailJob(
                                Type: odertype,
                                OrderId: order.Id,
                                ReferenceNo: order.ReferenceNo,
                                TxnId: txnId,
                                OccurredAt: TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, nzTz),
                                EmailQueueItemId: null
                            ));

                            logger.LogInformation("{HireOrBuyPaymentCompleted} job enqueued.Ref={Ref}, TxnId={TxnId}", odertype,
                               order.ReferenceNo, txnId);

                            logger.LogInformation("Paystation notify response: {Resp}", "OK_ENQUEUED");
                            return Results.Ok("OK");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to enqueue HirePaymentCompleted job. OrderId={OrderId}", order.Id);
                            logger.LogInformation("Paystation notify response: {Resp}", "OK_DB_SAVED_EMAIL_ENQUEUE_FAILED");
                            return Results.Ok("OK_DB_SAVED_EMAIL_ENQUEUE_FAILED");
                        }
                    
                }
            });
        }

        private static string ExtractRedirectUrl(string xml)
        {
            var s = xml.IndexOf("<DigitalOrder>", StringComparison.OrdinalIgnoreCase);
            var e = xml.IndexOf("</DigitalOrder>", StringComparison.OrdinalIgnoreCase);
            return s >= 0 && e > s
                ? xml.Substring(s + 14, e - s - 14)
                : "";
        }

        private static bool TryParseOrderIdFromMerchantReference(string? merchantReference, out int orderId)
        {
            orderId = 0;

            if (string.IsNullOrWhiteSpace(merchantReference))
                return false;

            if (!merchantReference.StartsWith("REF", StringComparison.OrdinalIgnoreCase))
                return false;

            return int.TryParse(merchantReference[3..], out orderId);
        }

        private static string SafeSnippet(string? text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= maxLen ? text : text[..maxLen] + "...";
        }

    }
}
