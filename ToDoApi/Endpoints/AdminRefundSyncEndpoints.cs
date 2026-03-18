using GownApi.Model;
using GownApi.Services.Paystation;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class AdminRefundSyncEndpoints
    {

        public static void MapAdminRefundSyncEndpoints(this WebApplication app)
        {
            // POST /api/admin/orders/{orderId}/refund/sync
            app.MapPost("/api/admin/orders/{orderId:int}/refund/sync", async (
                int orderId,
                GownDb db,
                PaystationQuickLookupClient client,
                HttpContext httpContext,
                ILogger<Program> logger) =>
            {
                var order = await db.orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order == null) return Results.NotFound("Order not found.");

                var merchantreference = $"RREF{order.Id}";
                httpContext.Items["MerchantReference"] = merchantreference;

                if (string.IsNullOrWhiteSpace(order.PaymentTxnId))
                    return Results.BadRequest("Missing payment_txn_id on order; cannot lookup refund status.");

                string xml;
                PaystationQuickLookupResult parsed;

                try
                {
                    xml = await client.LookupRawXmlByTxnIdAsync(order.PaymentTxnId);
                    logger.LogInformation("Refund sync lookup raw XML. OrderId={OrderId}, PaymentTxnId={PaymentTxnId}, Xml={Xml}",
                        orderId,
                        order.PaymentTxnId,
                        xml);

                    parsed = PaystationQuickLookupParser.Parse(xml);

                    logger.LogInformation("Refund sync parsed lookup. OrderId={OrderId}, LookupCode={LookupCode}, LookupMessage={LookupMessage}, TransactionProcess={TransactionProcess}, TotalSuccessfulRefunds={TotalSuccessfulRefunds}, PaystationErrorCode={PaystationErrorCode}, PaystationErrorMessage={PaystationErrorMessage}",
                        orderId,parsed.LookupCode,parsed.LookupMessage,parsed.TransactionProcess,parsed.TotalSuccessfulRefunds,parsed.PaystationErrorCode,parsed.PaystationErrorMessage);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Refund sync lookup failed. OrderId={OrderId}", orderId);
                    order.RefundLastEc = -1;
                    order.RefundLastEm = "Quick lookup call/parse failed.";

                    await db.SaveChangesAsync();

                    return Results.Problem("Lookup failed.");
                }

              
                if (!string.Equals(parsed.LookupCode, "00", StringComparison.OrdinalIgnoreCase))
                {
                    order.RefundLastEc = -2; 
                    order.RefundLastEm = $"Lookup failed: {parsed.LookupCode} {parsed.LookupMessage}".Trim();

                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        orderId = order.Id,
                        paymentTxnId = order.PaymentTxnId,
                        lookup = parsed,
                        lookupSummary = new
                        {
                            parsed.LookupCode,
                            parsed.LookupMessage,
                            parsed.TransactionProcess,
                            parsed.TotalSuccessfulRefunds,
                            parsed.PaystationErrorCode,
                            parsed.PaystationErrorMessage
                        },
                        refund = new
                        {
                            order.RefundStatusCode,
                            order.RefundedAmount,
                            order.RefundInitiatedAt,
                            order.RefundLastEc,
                            order.RefundLastEm
                        }
                    });
                }


                var totalSuccessfulRefunds = parsed.TotalSuccessfulRefunds ?? 0;
                var hasSuccessfulRefund = totalSuccessfulRefunds > 0;

                if (hasSuccessfulRefund)
                {
                    order.RefundStatusCode = RefundStatusCode.Completed;
                    order.Refunded = true;
                    order.RefundedAmount = Math.Round(totalSuccessfulRefunds / 100m, 2);

                    order.RefundLastEc = 0;
                    order.RefundLastEm = "Refund completed (synced via quick lookup).";
                }
                else
                {
                    if (order.RefundStatusCode == RefundStatusCode.InProgress)
                    {
                        order.RefundLastEc = 13;
                        order.RefundLastEm = "Lookup successful, but no successful refund found yet.";
                    }
                    else if (order.RefundStatusCode == RefundStatusCode.None)
                    {
                        order.RefundLastEc = 0;
                        order.RefundLastEm = "Lookup successful. Original purchase transaction is successful, and no refund has been recorded.";
                    }
                }

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    orderId = order.Id,
                    paymentTxnId = order.PaymentTxnId,
                    lookup = parsed,
                    lookupSummary = new
                    {
                        parsed.LookupCode,
                        parsed.LookupMessage,
                        parsed.TransactionProcess,
                        parsed.TotalSuccessfulRefunds,
                        parsed.PaystationErrorCode,
                        parsed.PaystationErrorMessage
                    },
                    refund = new
                    {
                        order.RefundStatusCode,
                        order.RefundedAmount,
                        order.RefundInitiatedAt,
                        order.RefundLastEc,
                        order.RefundLastEm
                    }
                });
            });
        }
    }
}
