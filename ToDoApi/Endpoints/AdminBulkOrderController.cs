using GownApi.Model;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class AdminBulkOrderController
    {
        public static void AdminBulkOrderEndpoints(WebApplication app)
        {
            app.MapGet("/admin/bulkorders", async (GownDb db) =>
            {
                return await db.bulkOrder.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync();

            });

            app.MapPost("/admin/bulkorders", async (List<BulkOrder> bulkOrders, GownDb db) =>
                {
                    db.bulkOrder.AddRangeAsync(bulkOrders);
                    await db.SaveChangesAsync();

                    return Results.Created("/admin/bulkorders", new {inserted = bulkOrders.Count });
                });
        }
    }
}
