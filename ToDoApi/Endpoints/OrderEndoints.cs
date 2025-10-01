using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
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
            {
                var result = await db.orders.ToListAsync();
                var resultList = new List<OrderDtoOut>();

                foreach (var res in result)
                {
                    var order = await OrderMapper.ToDtoOut(res, db);
                    resultList.Add(order);
                }
                return Results.Ok(resultList);
            });

        app.MapGet("/orders/{id}", async (int id, GownDb db, ILogger < Program > logger) =>
            {
                var order = await db.orders.FindAsync(id);

                logger.LogInformation("GET /orders/id called with ID={id}", id);
                if (order is null)
                {
                    logger.LogInformation("order is null");

                    return Results.NotFound();
                }
                var results = await OrderMapper.ToDtoOut(order, db);
                return Results.Ok(results);
            });

            app.MapPost("/orders", async (OrderDto orderDto, GownDb db) =>
            {
                var order = OrderMapper.FromDto(orderDto);

                db.orders.Add(order);
                db.SaveChanges();
                foreach (var item in orderDto.Items) {
                    var itemNew = await db.items.FindAsync(item.ItemId);
                    var orderedItems = new OrderedItems
                    {
                        OrderId = order.Id,
                        SkuId = item.ItemId, // It stores itemId, should store SkuId instead
                        Quantity = item.Quantity,
                        Hire = item.Hire,
                        Cost = item.Hire ? itemNew.HirePrice : itemNew.BuyPrice
                    };
                    db.orderedItems.Add(orderedItems);
                }

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
