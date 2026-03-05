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
                ILogger<Program> logger) =>
            {
                var order = await db.orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order == null) return Results.NotFound("Order not found.");

                if (string.IsNullOrWhiteSpace(order.PaymentTxnId))
                    return Results.BadRequest("Missing payment_txn_id on order; cannot lookup refund status.");

                string xml;
                PaystationQuickLookupResult parsed;

                try
                {
                    xml = await client.LookupRawXmlByTxnIdAsync(order.PaymentTxnId);
                    parsed = PaystationQuickLookupParser.Parse(xml);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Refund sync lookup failed. OrderId={OrderId}", orderId);

                    order.RefundLastEc = -1;
                    order.RefundLastEm = "Quick lookup call/parse failed.";
                    order.RefundStatusCode = RefundStatusCode.Failed;

                    await db.SaveChangesAsync();

                    return Results.Problem("Lookup failed.");
                }

              
                if (!string.Equals(parsed.LookupCode, "00", StringComparison.OrdinalIgnoreCase))
                {
                    order.RefundLastEc = 0; 
                    order.RefundLastEm = $"Lookup failed: {parsed.LookupCode} {parsed.LookupMessage}".Trim();
                    order.RefundStatusCode = RefundStatusCode.Failed;

                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        orderId = order.Id,
                        paymentTxnId = order.PaymentTxnId,
                        lookup = parsed,
                        refund = new
                        {
                            order.RefundStatusCode,
                            order.RefundedAmount,
                            order.RefundedAt,
                            order.RefundLastEc,
                            order.RefundLastEm
                        }
                    });
                }

         
                var hasSuccessfulRefund = (parsed.TotalSuccessfulRefunds ?? 0) > 0;

                if (hasSuccessfulRefund)
                {
                    order.RefundStatusCode = RefundStatusCode.Completed;
                    order.Refunded = true;

                    if (parsed.RefundAmountCents.HasValue && parsed.RefundAmountCents.Value > 0)
                        order.RefundedAmount = Math.Round(parsed.RefundAmountCents.Value / 100m, 2);

                  
                    order.RefundedAt ??= DateTimeOffset.UtcNow;

                    order.RefundLastEc = 0;
                    order.RefundLastEm = "Refund completed (synced via quick lookup).";
                }
                else
                {
               
                    if (order.RefundStatusCode == RefundStatusCode.InProgress)
                    {
                        order.RefundLastEc = 13;
                        order.RefundLastEm = "No successful refunds yet.";
                    }
                    else if (order.RefundStatusCode == RefundStatusCode.None)
                    {
                        order.RefundLastEc = 0;
                        order.RefundLastEm = "No refunds recorded.";
                    }
                
                }

                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    orderId = order.Id,
                    paymentTxnId = order.PaymentTxnId,
                    lookup = parsed,
                    refund = new
                    {
                        order.RefundStatusCode,
                        order.RefundedAmount,
                        order.RefundedAt,
                        order.RefundLastEc,
                        order.RefundLastEm
                    }
                });
            });
        }
    }
}
