using GownApi.Model;
using GownApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace GownApi.Endpoints
{
    public static class RefundEndpoints
    {
        record RefundRequest(decimal RefundAmount);
        private record RefundHttpResult(HttpStatusCode StatusCode, string ResponseText);
        public static void MapRefundEndpoints(this WebApplication app)
        {
            app.MapPost("/api/orders/{orderId:int}/refund-request", async (
                int orderId,
                RefundRequest request,
                GownDb db,
                HttpContext httpContext,
                ILogger<Program> logger) =>
            {
                if (request.RefundAmount <= 0)
                    return Results.BadRequest("Invalid refund amount.");

                   Orders? order;
                try {
                     order = await db.orders.FirstOrDefaultAsync(o => o.Id == orderId);
                }catch(Exception ex)
                {
                    logger.LogError(ex, "Database error while fetching order for refund request. OrderId={OrderId}", orderId);
                    return Results.Problem("Database error while fetching order.");
                }

                if (order == null) return Results.NotFound("Order not found.");
                // Refunded / in progress / requested are not allowed to request again

                var merchantreference = $"RREF{order.Id}";
                httpContext.Items["MerchantReference"] = merchantreference; 

                using(Serilog.Context.LogContext.PushProperty("MerchantReference", merchantreference))
                {
                    logger.LogInformation("Refund request initiated. OrderId={OrderId}, Amount={Amount}, By={By}",
                        order.Id, request.RefundAmount, httpContext.User.Identity?.Name ?? "");
                

                if (request.RefundAmount > order.AmountPaid)
                    return Results.BadRequest("Refund amount cannot exceed amount paid.");

                if (order.Paid is not true)
                    return Results.BadRequest("Only paid orders can be refunded.");

                if (order.Refunded || order.RefundStatusCode == RefundStatusCode.Completed)
                    return Results.Conflict("Order already refunded.");

                if (order.RefundStatusCode == RefundStatusCode.InProgress)
                    return Results.Conflict("Refund already in progress.");

                if (order.RefundStatusCode == RefundStatusCode.Requested)
                    return Results.Conflict("Refund already requested.");

                // write initial refund request status
                order.RefundStatusCode = RefundStatusCode.Requested;
                order.RefundedAmount = request.RefundAmount;
                order.RefundInitiatedAt = null;
                order.RefundTxnId = null;
                order.RefundLastEc = null;
                order.RefundLastEm = null;

                try{
                    await db.SaveChangesAsync(); 
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Database error while saving refund request. OrderId={OrderId}", orderId);
                    return Results.Problem("Database error while saving refund request.");
                }

                logger.LogInformation("Refund requested. OrderId={OrderId}, Amount={Amount}, By={By}",
                    order.Id, request.RefundAmount, httpContext.User.Identity?.Name ?? "");
                }

                return Results.Ok(new
                {
                    orderId = order.Id,
                    refundStatusCode = order.RefundStatusCode,
                    amount = order.RefundedAmount
                });
            })
                .RequireAuthorization();

            app.MapPost("/api/orders/{orderId:int}/refund-approve", async (
                int orderId,
                RefundRequest request,
                GownDb db,
                IOptions < PaystationOptions > paystationOptions,
                IHttpClientFactory httpClientFactory,
                IQueueJobPublisher publisher,
                ILogger<Program> logger,
                HttpContext httpContext) =>
            {
                if (request.RefundAmount <= 0)
                    return Results.BadRequest("Invalid refund amount.");

                Orders? order;
                try {
                     order = await db.orders.FirstOrDefaultAsync(o => o.Id == orderId);
                }catch(Exception ex)
                {
                    logger.LogError(ex, "Database error while fetching order for refund approval. OrderId={OrderId}", orderId);
                    return Results.Problem("Database error while fetching order.");
                }   
                if (order == null) return Results.NotFound("Order not found.");

                var merchantreference = $"RREF{order.Id}";
                httpContext.Items["MerchantReference"] = merchantreference;

                if (request.RefundAmount > order.AmountPaid)
                    return Results.BadRequest("Refund amount cannot exceed amount paid.");

                if (order.Paid is not true)
                    return Results.BadRequest("Only paid orders can be refunded.");

                // 1) Prevent duplicate refunds
                if (order.Refunded || order.RefundStatusCode == RefundStatusCode.Completed)
                    return Results.Conflict("Order already refunded.");

                // 2) Request -> Approve flow: only allowed if currently in "Requested" state
                if (order.RefundStatusCode != RefundStatusCode.Requested)
                    return Results.Conflict($"Refund is not in requested state. Current={order.RefundStatusCode}");

                if (request.RefundAmount != order.RefundedAmount)
                    return Results.BadRequest("Approved refund amount must match requested amount.");

                // 3) txn id needed for refund API call
                if (string.IsNullOrWhiteSpace(order.PaymentTxnId))
                    return Results.BadRequest("Missing payment_txn_id on order; cannot refund.");

                var settings = paystationOptions.Value;
                // Paystation config
                var paystationId = settings.PaystationId;
                var gatewayId = settings.GatewayId;
                var hmacKey = settings.HmacKey;
                var refundUrl = settings.RefundUrl;

                if (string.IsNullOrWhiteSpace(paystationId) || string.IsNullOrWhiteSpace(gatewayId))
                    return Results.Problem("Paystation config missing (PaystationId/GatewayId).");

                if (string.IsNullOrWhiteSpace(hmacKey))
                    return Results.Problem("Paystation config missing (HmacKey).");

                // dollars -> cents
                if (decimal.Round(request.RefundAmount, 2) != request.RefundAmount)
                    return Results.BadRequest("Refund amount must have at most 2 decimal places.");

                //var amountCents = decimal.ToInt32(request.RefundAmount * 100m);
                var amountCents = Convert.ToInt32(Math.Round(request.RefundAmount * 100m, MidpointRounding.AwayFromZero));

                // Merchant session
                //var merchantSession = order.ReferenceNo.Trim();
                //if (string.IsNullOrWhiteSpace(merchantSession)) 
                  //  return Results.BadRequest("Paid order must have merchantSession.");
                //var refundMerchantSession = "Re" + merchantSession;
                var refundMerchantReference = $"RREF{order.Id}";
                var refundMerchantSession = $"RREF{order.Id}-{Guid.NewGuid():N}";


                // Set status to InProgress before calling Paystation, to prevent duplicate
                order.RefundStatusCode = RefundStatusCode.InProgress;
                order.RefundLastEc = null;
                order.RefundLastEm = null;
                order.Refunded = false;
                order.RefundedAmount = request.RefundAmount;
                order.RefundInitiatedAt = null;
                order.RefundTxnId = null;

                try
                {
                    await db.SaveChangesAsync();
                    logger.LogInformation("Refund approved -> in progress. OrderId={OrderId}, Amount={Amount}, ApprovedBy={By}",
                        order.Id, request.RefundAmount, httpContext.User.Identity?.Name ?? "");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to save refund approve (in progress). OrderId={OrderId}", order.Id);
                    return Results.Problem("Failed to save refund approval.");
                }

                // ===== Build form body =====
                var pairs = BuildRefundRequestPairs(
                    paystationId,
                    gatewayId,
                    refundMerchantSession,
                    amountCents,
                    order.PaymentTxnId,
                    refundMerchantReference
                    );

                var content = new FormUrlEncodedContent(pairs);
                var bodyString = await content.ReadAsStringAsync();

                // ===== HMAC =====
                var unixTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
                var stringToHash = unixTs + "paystation" + bodyString;

                var hmacHex = HmacSha512Hex(hmacKey, stringToHash);

                var url = BuildRefundUrl(refundUrl, unixTs, hmacHex);

                string respText;
                HttpStatusCode httpStatus;

                try
                {
                    var httpResult = await PostRefundToPaystationAsync(httpClientFactory, url, content);
                    httpStatus = httpResult.StatusCode;
                    respText = httpResult.ResponseText;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Paystation refund call failed (http). OrderId={OrderId}", order.Id);
                    order.RefundLastEc = -1;
                    order.RefundLastEm = "Paystation refund call failed (http).";
                    order.RefundStatusCode = RefundStatusCode.Failed;
                    await db.SaveChangesAsync();
                    return Results.Problem("Paystation refund call failed.");
                }

                using (Serilog.Context.LogContext.PushProperty("MerchantReference", merchantreference))
                {
                    logger.LogInformation("Paystation refund response. HttpStatus={HttpStatus}, BodySnippet={BodySnippet}",
                        (int)httpStatus, SafeSnippet(respText, 3000));
                }

                if (!((int)httpStatus >= 200 && (int)httpStatus < 300))
                {
                    order.RefundLastEc = -2;
                    order.RefundLastEm = $"Paystation HTTP {(int)httpStatus}";
                    order.RefundStatusCode = RefundStatusCode.Failed;
                    await db.SaveChangesAsync();
                    return Results.Problem($"Paystation returned HTTP {(int)httpStatus}.");
                }

                var (ec, em, refundTxnId,refundTxnTime) = ParsePaystationResponse(respText);
                var parsedRefundInitiatedAtUtc = ParsePaystationTransactionTimeUtc(refundTxnTime);

                order.RefundLastEc = ec;
                order.RefundLastEm = em;
                var occurredAt = parsedRefundInitiatedAtUtc.HasValue? new DateTimeOffset(parsedRefundInitiatedAtUtc.Value): DateTimeOffset.UtcNow;

                if (ec == 0)
                {
                    order.RefundStatusCode = RefundStatusCode.Completed;
                    order.Refunded = true;
                    order.RefundInitiatedAt = parsedRefundInitiatedAtUtc ?? DateTime.UtcNow;
                    order.RefundTxnId = string.IsNullOrWhiteSpace(refundTxnId) ? null : refundTxnId;
                   

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Refund succeeded at Paystation but failed to persist locally. OrderId={OrderId}, RefundTxnId={RefundTxnId}",
                            order.Id, order.RefundTxnId);

                        return Results.Problem("Refund succeeded externally but local update failed. Please run refund sync.");
                    }

                    await publisher.EnqueueEmailJobAsync(new EmailJob(
                        Type: "RefundCompleted",
                        OrderId: order.Id,
                        ReferenceNo: order.ReferenceNo,
                        TxnId: order.RefundTxnId,
                        OccurredAt: occurredAt,
                        EmailQueueItemId: null
                    ));

                    return Results.Ok(new
                    {
                        orderId = order.Id,
                        amount = order.RefundedAmount,
                        paymentTxnId = order.PaymentTxnId,
                        refundTxnId = order.RefundTxnId,
                        refundInitiatedAt = order.RefundInitiatedAt,
                        ec,
                        em
                    });
                }

                if (ec == 13)
                {
                    order.RefundStatusCode = RefundStatusCode.InProgress;
                    order.Refunded = false;
                    order.RefundTxnId = string.IsNullOrWhiteSpace(refundTxnId) ? null : refundTxnId;

                    await db.SaveChangesAsync();

                    await publisher.EnqueueEmailJobAsync(new EmailJob(
                        Type: "RefundInProgress",
                        OrderId: order.Id,
                        ReferenceNo: order.ReferenceNo,
                        TxnId: order.RefundTxnId,
                        OccurredAt: DateTimeOffset.Now,
                        EmailQueueItemId: null
                    ));

                    return Results.Ok(new
                    {
                        orderId = order.Id,                  
                        amount = order.RefundedAmount,
                        paymentTxnId = order.PaymentTxnId,
                        refundTxnId = order.RefundTxnId,
                        ec,
                        em 
                    });

                }

                order.RefundStatusCode = RefundStatusCode.Failed;
                order.Refunded = false;

                await db.SaveChangesAsync();

                return Results.BadRequest(new
                {
                    orderId = order.Id,
                    refundStatusCode = order.RefundStatusCode,
                    paymentTxnId = order.PaymentTxnId,
                    ec,
                    em 
                });
            })
                .RequireAuthorization(policy => policy.RequireRole("manager"));
        }

        private static List<KeyValuePair<string, string>> BuildRefundRequestPairs(
            string paystationId,
            string gatewayId,
            string merchantSession,
            int amountCents,
            string paymentTxnId,
            string merchantReference
            )
        {
            return new List<KeyValuePair<string, string>>
            {
                new("paystation", "_empty"),
                new("pstn_nr", "t"),
                new("pstn_pi", paystationId),
                new("pstn_gi", gatewayId),
                new("pstn_ms", merchantSession),
                new("pstn_am", amountCents.ToString(CultureInfo.InvariantCulture)),
                new("pstn_2p", "t"),
                new("pstn_rc", "t"),
                new("pstn_rt", paymentTxnId),
                new("pstn_tm", "t"),
                new("pstn_mr", merchantReference),
                new("pstn_rf", "JSON")
            };
        }

        private static string HmacSha512Hex(string key, string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string BuildRefundUrl(string refundURL, string unixTs, string hmacHex)
        {
            return
                refundURL
                + $"?pstn_HMACTimestamp={WebUtility.UrlEncode(unixTs)}"
                + $"&pstn_HMAC={WebUtility.UrlEncode(hmacHex)}";
        }

        private static async Task<RefundHttpResult> PostRefundToPaystationAsync(
            IHttpClientFactory httpClientFactory,
            string url,
            FormUrlEncodedContent content)
        {
            var httpClient = httpClientFactory.CreateClient();
            var resp = await httpClient.PostAsync(url, content);
            var respText = await resp.Content.ReadAsStringAsync();

            return new RefundHttpResult(resp.StatusCode, respText);
        }

        private static string SafeSnippet(string? text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            return text.Length <= maxLen ? text : text[..maxLen] + "...";
        }
        private static (int ec, string? em, string? ReTxnId,string? ReTxnTime) ParsePaystationResponse(string respText)
        {
            int ec = -1;
            string? em = null;
            string? ReTxnId = null;
            string? ReTxnTime = null;

            try
            {
                using var json = JsonDocument.Parse(respText);
                var root = json.RootElement;

                if (root.TryGetProperty("PaystationRefundResponse", out var wrapped))
                    root = wrapped;

                if (root.TryGetProperty("ec", out var ecEl))
                {
                    if (ecEl.ValueKind == JsonValueKind.Number)
                        ec = ecEl.GetInt32();
                    else if (ecEl.ValueKind == JsonValueKind.String && int.TryParse(ecEl.GetString(), out var ec2))
                        ec = ec2;
                }

                if (root.TryGetProperty("em", out var emEl))
                    em = emEl.GetString();

                if (root.TryGetProperty("TransactionTime", out var ttEl) &&
                    ttEl.ValueKind == JsonValueKind.String)
                {
                    ReTxnTime = ttEl.GetString();
                }

                if (root.TryGetProperty("ti", out var tiEl))
                    ReTxnId = tiEl.GetString();
                else if (root.TryGetProperty("TransactionID", out var t2))
                    ReTxnId = t2.GetString();
                else if (root.TryGetProperty("PaystationTransactionID", out var t3))
                    ReTxnId = t3.GetString();

                return (ec, em, ReTxnId, ReTxnTime);
            }
            catch { }

            try
            {
                var x = XDocument.Parse(respText);
                string? Get(string n) => x.Root?.Element(n)?.Value;

                ec = int.TryParse(Get("ec"), out var v) ? v : -1;
                em = Get("em");
                ReTxnId = Get("ti") ?? Get("TransactionID") ?? Get("PaystationTransactionID");
                ReTxnTime = Get("TransactionTime");

                return (ec, em, ReTxnId, ReTxnTime);
            }
            catch
            {
                return (-1, null, null,null);
            }
        }

        private static DateTime? ParsePaystationTransactionTimeUtc(string? rawTime)
        {
            if (string.IsNullOrWhiteSpace(rawTime))
                return null;

            if (DateTime.TryParseExact(
                rawTime,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
            {
                TimeZoneInfo nzTz;
                try
                {
                    nzTz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
                }
                catch
                {
                    nzTz = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
                }

                var nzLocal = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(nzLocal, nzTz);
                return utc;
            }

            return null;
        }
    }
}