using GownApi.Model.Dto;
using GownApi.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace GownApi.Endpoints
{
    public static class DeliveryEndpoints
    {
        public const int DELIVERY_ID = 21;
        public static void MapDeliveryEndpoints(this WebApplication app)
        {
         app.MapGet("/delivery", async (GownDb db) =>
            {
                var results = await db.itemDegreeModels
                    .FromSqlRaw(@"SELECT i.id, NULL as degree_id, NULL as degree_name, NULL as degree_order, i.name, i.picture, 0 as hire_price, 0 as buy_price, i.category, i.description, i.is_hiring, NULL as active
                    FROM public.items i
                    WHERE i.id = {0}", DELIVERY_ID)
                    .ToListAsync();

                var itemsDto = new List<ItemDto>();

                foreach (var i in results)
                {
                    var itemDto = await Utils.GetOptions(i, db);
                    itemsDto.Add(itemDto);
                }

                var itemsDtoList = itemsDto.ToList();
                return Results.Ok(itemsDtoList);
            });

            app.MapPut("/delivery/{id}", async (int id, DeliveryDto updatedDelivery, GownDb db, ILogger<Program> logger) =>
            {
                logger.LogInformation("PUT /deliverys/id called with ID={id}, Body={@updatedDelivery}", id, updatedDelivery);

                if (id != updatedDelivery.Id)
                    return Results.BadRequest("ID in URL and body must match");

                var delivery = await db.items.FindAsync(id);
                if (delivery is null)
                    return Results.NotFound();

                delivery.Name = updatedDelivery.Name;

                await db.SaveChangesAsync();
                return Results.Ok(delivery);
            });


            app.MapPut("/delivery/cost/{id}", async (int id, DeliveryDto updatedDelivery, GownDb db, ILogger<Program> logger) =>
            {
                logger.LogInformation("PUT /deliverys/cost/id called with ID={id}, Body={@updatedDelivery}", id, updatedDelivery);

                if (id != updatedDelivery.Id)
                    return Results.BadRequest("ID in URL and body must match");

                var delivery = await db.sizes.FindAsync(id);
                if (delivery is null)
                    return Results.NotFound();

                delivery.Size = updatedDelivery.Name;
                delivery.Price = updatedDelivery.Cost;

                await db.SaveChangesAsync();
                return Results.Ok(delivery);
            });
        }
    }
}
