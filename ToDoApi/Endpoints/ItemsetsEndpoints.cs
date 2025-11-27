using GownApi.Model.Dto;
using GownApi.Services;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class ItemsetsEndpoints
    {
        public static void MapItemsetsEndpoints(this WebApplication app)
        {
            app.MapGet("/itemsets", async (GownDb db) => {
                var results = await db.itemDegreeModels
                .FromSqlRaw(@"SELECT i.id, NULL as degree_id, NULL as degree_name, NULL as degree_order, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring
                    FROM public.items i
                    WHERE category='Set'")
                .ToListAsync();

                var itemsDto = new List<ItemDto>();
                foreach (var i in results)
                {
                    var itemDto = await Utils.GetSetOptions(i, db);
                    itemsDto.Add(itemDto);
                }

                //var itemsDtoResult = ItemMapper.ToDtoList(itemsDto);
                return Results.Ok(itemsDto);
            });
        }
    }
}
