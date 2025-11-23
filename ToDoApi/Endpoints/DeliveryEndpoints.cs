using GownApi.Model.Dto;
using GownApi.Services;
using Microsoft.EntityFrameworkCore;

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
                    .FromSqlRaw(@"SELECT i.id, NULL as degree_id, NULL as degree_name, NULL as degree_order, i.name, i.picture, 0 as hire_price, 0 as buy_price, i.category, i.description, i.is_hiring
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
        }
    }
}
