using DocumentFormat.OpenXml.Drawing.Charts;
using GownApi.Model;
using GownApi.Services;
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
        record PaymentRequest(int PayAmount, string OrderNo);

        public static void MapPaymentEndpoints(this WebApplication app)
        {
            app.MapPost("/api/payment/create-payment", async (
                PaymentRequest request,
                IHttpClientFactory httpClientFactory,
                IQueueJobPublisher publisher,
                IOptions<PaystationOptions> paystationOptions,
                ILogger<Program> logger,
                HttpContext httpContext,
                CancellationToken ct
                ) =>
            {
                if (request.PayAmount <= 0 || string.IsNullOrWhiteSpace(request.OrderNo))
                {
                    logger.LogWarning("Invalid create-payment request:Amount={PayAmount}, OrderNo={OrderNumber}",
                        request.PayAmount, request.OrderNo);
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

                var orderNo = request.OrderNo.Trim();
                var merchantSession = orderNo;
                var merchantReference = orderNo;
                httpContext.Items["OrderNo"] = orderNo;

                using (Serilog.Context.LogContext.PushProperty("OrderNo", orderNo))
                using (Serilog.Context.LogContext.PushProperty("MerchantSession", merchantSession))
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
                                ReferenceNo: orderNo,
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
                              string.IsNullOrWhiteSpace(redirectUrl),
                              SafeSnippet(responseString, 6000));

                        return Results.Ok(new { redirectUrl });
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {

                        logger.LogInformation("Create-payment canceled by client. OrderNo={OrderNo}", orderNo);
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
                                ReferenceNo: orderNo,
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
                                ReferenceNo: orderNo,
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
                var orderNo = merchantSession;
                var requestTime = Get("TransactionTime") ?? Get("PaymentRequestTime");
                var receiptTime = Get("DigitalOrderTime") ?? Get("DigitalReceiptTime");
                var purchaseAmountCents = int.TryParse(Get("PurchaseAmount"), out var cents) ? cents : 0;
                var purchaseAmount = purchaseAmountCents / 100m;
                var receiptNumber = Get("ReturnReceiptNumber");

                using (Serilog.Context.LogContext.PushProperty("OrderNo", orderNo ?? ""))
                using (Serilog.Context.LogContext.PushProperty("MerchantSession", merchantSession ?? ""))
                using (Serilog.Context.LogContext.PushProperty("PaystationTxnId", txnId ?? ""))
                using (Serilog.Context.LogContext.PushProperty("PaystationEC", ec))
                using (Serilog.Context.LogContext.PushProperty("AmountCents", purchaseAmountCents))
                using (Serilog.Context.LogContext.PushProperty("ReceiptNo", receiptNumber ?? ""))
                {
                    logger.LogInformation("Paystation notify received. ec={EC}, em={EM}, requestTime={RequestTime}, receiptTime={ReceiptTime},amountcents={Amount}, receipt={Receipt}",
                        ec, em, requestTime, receiptTime, purchaseAmountCents, receiptNumber);

                    if (string.IsNullOrWhiteSpace(merchantSession))
                    {
                        logger.LogWarning("Paystation notify missing MerchantSession. BodySnippet={BodySnippet}", SafeSnippet(raw, 600));
                        logger.LogInformation("Paystation notify response: {Resp}", "NO_MERCHANT_SESSION");
                        return Results.Ok("NO_MERCHANT_SESSION");
                    }

                    Orders? order;
                    try
                    {
                        order = await db.orders
                            .FirstOrDefaultAsync(o => o.Id == Convert.ToInt32(merchantReference));
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

                    using (Serilog.Context.LogContext.PushProperty("OrderId", order.Id))
                    {
                        order.Paid = (ec == 0);

                        if (ec == 0)
                        {
                            order.AmountPaid = purchaseAmount;
                            //Original Paystation payment transaction id for future refunds
                            order.PaymentTxnId = txnId;
                        }
                        else
                        {
                            order.AmountPaid = 0;
                        }

                        try
                        {
                            await db.SaveChangesAsync();

                            // confirm saved
                            logger.LogInformation("Order payment saved. OrderId={OrderId}, Paid={Paid}, AmountPaidCents={AmountPaid}, PaymentTxnId={PaymentTxnId}",
                                order.Id, order.Paid, order.AmountPaid, order.PaymentTxnId);
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

                        try
                        {
                            await publisher.EnqueueEmailJobAsync(new EmailJob(
                                Type: "PaymentCompleted",
                                OrderId: order.Id,
                                ReferenceNo: order.ReferenceNo ?? merchantSession ?? order.Id.ToString(),
                                TxnId: txnId,
                                OccurredAt: TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, nzTz),
                                EmailQueueItemId: null
                            ));

                            logger.LogInformation("PaymentCompleted job enqueued. OrderId={OrderId}, Ref={Ref}, TxnId={TxnId}",
                                order.Id, order.ReferenceNo, txnId);

                            logger.LogInformation("Paystation notify response: {Resp}", "OK_ENQUEUED");
                            return Results.Ok("OK");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to enqueue PaymentCompleted job. OrderId={OrderId}", order.Id);
                            logger.LogInformation("Paystation notify response: {Resp}", "OK_DB_SAVED_EMAIL_ENQUEUE_FAILED");
                            return Results.Ok("OK_DB_SAVED_EMAIL_ENQUEUE_FAILED");
                        }
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

        private static string SafeSnippet(string? text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= maxLen ? text : text[..maxLen] + "...";
        }

    }
}
