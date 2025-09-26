using GownApi.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace GownApi.Endpoints
{
    public static class OrderEndoints
    {
        public static void MapOrderEnpoints(this WebApplication app)
        {
            app.MapGet("/orders", async (GownDb db) =>
                await db.orders.ToListAsync());

            app.MapGet("/orders/{id}", async (int id, GownDb db, ILogger < Program > logger) =>
            {
                var order = await db.orders.FindAsync(id);

                logger.LogInformation("GET /orders/id called with ID={id}", id);
                if (order is null)
                    return Results.NotFound();
                return Results.Ok(order);
            });

            app.MapPost("/orders", async (Orders order, GownDb db) =>
            {
                db.orders.Add(order);
                await db.SaveChangesAsync();

                return Results.Created($"/orders/{order.Id}", order);
            });

            app.MapPut("/orders/{id}", async (int id, Orders updatedOrder, GownDb db, ILogger<Program> logger) =>
            {
                if (id != updatedOrder.Id)
                    return Results.BadRequest("ID in URL and body must match");

                logger.LogInformation("GET /orders/id called with ID={id}", id);
                var order = await db.orders.FindAsync(id);
                if (order is null)
                    return Results.NotFound();

                // Update fields
                order.FirstName = updatedOrder.FirstName;
                order.LastName = updatedOrder.LastName;
                order.Email = updatedOrder.Email;
                order.Address = updatedOrder.Address;
                order.City = updatedOrder.City;
                order.Postcode = updatedOrder.Postcode;
                order.Country = updatedOrder.Country;
                order.Phone = updatedOrder.Phone;
                order.Mobile = updatedOrder.Mobile;
                order.StudentId = updatedOrder.StudentId;
                order.Message = updatedOrder.Message;
                order.Paid = updatedOrder.Paid;
                order.PaymentMethod = updatedOrder.PaymentMethod;
                order.PurchaseOrder = updatedOrder.PurchaseOrder;
                order.OrderDate = updatedOrder.OrderDate;

                await db.SaveChangesAsync();
                return Results.Ok(order);
            });
        }
    }
}
