using GownsApi;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class OrderEndoints
    {
        public static void MapOrderEnpoints(this WebApplication app)
        {
            app.MapGet("/orders", async (GownDb db) =>
                await db.orders.ToListAsync());

            app.MapPost("/orders", async (Orders order, GownDb db) =>
            {
                db.orders.Add(order);
                await db.SaveChangesAsync();

                return Results.Created($"/orders/{order.Id}", order);
            });
        }
    }
}
