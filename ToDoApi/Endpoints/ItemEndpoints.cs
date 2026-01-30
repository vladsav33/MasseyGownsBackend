using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using GownsApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class ItemEndpoints
    {
        const int CASUAL_HIRE_PHOTO = 2;
        public static void MapItemEndpoints(this WebApplication app)
        {
            app.MapGet("/items/{id}", async (int id, GownDb db) => {
                var results = await db.Database.SqlQueryRaw<ItemDegreeModel>(@"
                    SELECT DISTINCT i.id, cd.degree_id, d.name as degree_name, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring, cdi.active
                    FROM public.items i
                    INNER JOIN public.ceremony_degree_item cdi ON i.id = cdi.item_id
                    INNER JOIN public.ceremony_degree cd on cdi.ceremony_degree_id = cd.id
                    INNER JOIN public.degrees d ON cd.degree_id = d.id
                    WHERE i.id = {0}", id)
                .ToListAsync();

                var itemsDto = ItemMapper.ToDtoList(results);
                return Results.Ok(itemsDto);
            });

            app.MapGet("/items", async (GownDb db) => {
                var results = await db.Set<ItemDegreeModel>()
                .FromSqlRaw(@"SELECT DISTINCT i.id, g.degree_id, d.name as degree_name, d.degree_order, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring, cdi.active
                    FROM public.ceremony_degree g
                    INNER JOIN public.ceremony_degree_item cdi ON g.id = cdi.ceremony_degree_id
                    INNER JOIN public.items i ON cdi.item_id = i.id
                    INNER JOIN public.degrees d ON g.degree_id = d.id
                    WHERE cdi.active AND g.graduation_id={0}", CASUAL_HIRE_PHOTO)
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
            app.MapGet("/itemsonly", async (GownDb db) => {
                return await db.items.ToListAsync();
            });

            app.MapGet("/sizesonly", async (GownDb db) => {
                return await db.sizes.ToListAsync();
            });

            app.MapGet("/hoodsonly", async (GownDb db) => {
                return await db.hood_type.ToListAsync();
            });

            app.MapGet("/itemsbydegree/{id}", async (int id, GownDb db) =>
            {
                var results = await db.itemDegreeModels
                    .FromSqlRaw(@"SELECT i.id, cd.degree_id as degree_id, NULL as degree_name, NULL as degree_order, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring, cdi.active
                    FROM public.ceremony_degree_item cdi
                    INNER JOIN public.items i on cdi.item_id = i.id
					INNER JOIN public.ceremony_degree cd on cdi.ceremony_degree_id = cd.id
                    WHERE cdi.active AND cdi.ceremony_degree_id = {0}", id)
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

            app.MapGet("/admin/sku", async (GownDb db) =>
            {
                var results = await db.skuDetail
                    .FromSqlRaw(@"SELECT sk.id, i.name, s.size, f.fit_type, h.name as hood, sk.count as count FROM sku sk
                                  LEFT JOIN items i
                                  ON i.id = sk.item_id
                                  LEFT JOIN sizes s
                                  ON s.id = sk.size_id
                                  LEFT JOIN fit f
                                  ON f.id = sk.fit_id
                                  LEFT JOIN hood_type h
                                  ON h.id = sk.hood_id
                                  WHERE i.category <> 'Delivery' AND i.category <> 'Set'
                                  ORDER BY i.name, f.fit_type, s.display_order, h.name;")
                    .ToListAsync();
                return Results.Ok(results);
            });
        }
    }
}
