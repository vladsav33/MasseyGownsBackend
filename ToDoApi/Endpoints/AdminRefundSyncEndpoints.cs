using GownApi;
using GownApi.Model;
using GownApi.Services.Paystation;
using Microsoft.EntityFrameworkCore;

public static class AdminRefundSyncEndpoints
{
    private static object BuildRefundDto(Orders order) => new
    {
        order.RefundStatusCode,
        order.Refund,
        order.RefundedAmount,
        order.RefundInitiatedAt,
        order.RefundLastEc,
        order.RefundLastEm
    };

    private static object BuildLookupDto(PaystationQuickLookupResult parsed) => new
    {
        parsed.LookupCode,
        parsed.LookupMessage,
        parsed.TransactionProcess,
        parsed.TotalSuccessfulRefunds,
        parsed.PaystationErrorCode,
        parsed.PaystationErrorMessage
    };

    private static IResult RefundSyncOk(Orders order, PaystationQuickLookupResult? parsed = null)
    {
        if (parsed == null)
        {
            return Results.Ok(new
            {
                refund = BuildRefundDto(order)
            });
        }

        return Results.Ok(new
        {
            lookup = BuildLookupDto(parsed),
            refund = BuildRefundDto(order)
        });
    }

    public static void MapAdminRefundSyncEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/orders/{orderId:int}/refund/sync", async (
            int orderId,
            GownDb db,
            PaystationQuickLookupClient client,
            HttpContext httpContext,
            ILogger<Program> logger) =>
        {
            var order = await db.orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return Results.NotFound("Order not found.");

            var merchantreference = $"CRREF{order.Id}";
            httpContext.Items["MerchantReference"] = merchantreference;

            using (Serilog.Context.LogContext.PushProperty("MerchantReference", merchantreference))
            {
                if (string.IsNullOrWhiteSpace(order.PaymentTxnId))
                {
                    logger.LogInformation("Order is unpaid. OrderId={OrderId}", orderId);
                    return Results.BadRequest(new 
                    {
                        message = "The order is unpaid.",
                        refund = BuildRefundDto(order)
                    });
                }

                if (order.RefundStatusCode == RefundStatusCode.None)
                {
                    logger.LogInformation("No refund record found. OrderId={OrderId}", orderId);
                    return Results.Ok(new
                    {
                        message = "No refund record found.",
                        refund = BuildRefundDto(order)
                    });
                }

                if (order.RefundStatusCode == RefundStatusCode.Requested)
                {
                    logger.LogInformation("Refund has not been approved yet. OrderId={OrderId}", orderId);
                    return RefundSyncOk(order);
                }

                string xml;
                PaystationQuickLookupResult parsed;

                try
                {
                    xml = await client.LookupRawXmlByTxnIdAsync(order.PaymentTxnId);

                    logger.LogInformation(
                        "Refund sync lookup raw XML. OrderId={OrderId}, PaymentTxnId={PaymentTxnId}, Xml={Xml}",
                        orderId,
                        order.PaymentTxnId,
                        xml
                    );

                    parsed = PaystationQuickLookupParser.Parse(xml);

                    logger.LogInformation(
                        "Refund sync parsed lookup. OrderId={OrderId}, LookupCode={LookupCode}, LookupMessage={LookupMessage}, TransactionProcess={TransactionProcess}, TotalSuccessfulRefunds={TotalSuccessfulRefunds}, PaystationErrorCode={PaystationErrorCode}, PaystationErrorMessage={PaystationErrorMessage}",
                        orderId,
                        parsed.LookupCode,
                        parsed.LookupMessage,
                        parsed.TransactionProcess,
                        parsed.TotalSuccessfulRefunds,
                        parsed.PaystationErrorCode,
                        parsed.PaystationErrorMessage
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Refund sync lookup failed. OrderId={OrderId}", orderId);
                    return Results.Problem("Lookup failed.");
                }

                if (!string.Equals(parsed.LookupCode, "00", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(
                        "Refund sync lookup returned non-success code. OrderId={OrderId}, LookupCode={LookupCode}, LookupMessage={LookupMessage}",
                        orderId,
                        parsed.LookupCode,
                        parsed.LookupMessage
                    );

                    return RefundSyncOk(order, parsed);
                }

                var totalSuccessfulRefunds = parsed.TotalSuccessfulRefunds ?? 0;
                var hasSuccessfulRefund = totalSuccessfulRefunds > 0;

                if (hasSuccessfulRefund)
                {
                    order.RefundStatusCode = RefundStatusCode.Completed;
                    order.Refunded = true;
                    order.RefundedAmount = Math.Round(totalSuccessfulRefunds / 100m, 2);

                    await db.SaveChangesAsync();

                    logger.LogInformation(
                        "Refund sync marked order as completed. OrderId={OrderId}, RefundedAmount={RefundedAmount}",
                        orderId,
                        order.RefundedAmount
                    );
                }
                else
                {
                    logger.LogInformation(
                        "Refund sync found no successful refund yet. OrderId={OrderId}, CurrentRefundStatusCode={RefundStatusCode}",
                        orderId,
                        order.RefundStatusCode
                    );
                }

                return RefundSyncOk(order, parsed);
            }
        });
    }
}