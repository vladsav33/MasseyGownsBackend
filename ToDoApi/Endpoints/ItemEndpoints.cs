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
        public static void MapItemEndpoints(this WebApplication app)
        {
            app.MapGet("/items/{id}", async (int id, GownDb db) => {
                var results = await db.Database.SqlQueryRaw<ItemDegreeModel>(@"
                    SELECT DISTINCT i.id, cd.degree_id, d.name as degree_name, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring
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
                //.FromSqlRaw(@"SELECT i.id, NULL as degree_id, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring
                //    FROM public.items i")
                //.ToListAsync();
                .FromSqlRaw(@"SELECT DISTINCT i.id, g.degree_id, d.name as degree_name, d.degree_order, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring
                    FROM public.ceremony_degree g
                    INNER JOIN public.ceremony_degree_item cdi ON g.id = cdi.ceremony_degree_id
                    INNER JOIN public.items i ON cdi.item_id = i.id
                    INNER JOIN public.degrees d ON g.degree_id = d.id")
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

            app.MapPost("/items", async (ItemDto itemDto, GownDb db) =>
            {
                var item = new Items
                {
                    Id = itemDto.Id,
                    Name = itemDto.Name,
                    Picture = string.IsNullOrEmpty(itemDto.PictureBase64) ? null : Convert.FromBase64String(itemDto.PictureBase64),
                    HirePrice = itemDto.HirePrice,
                    BuyPrice = itemDto.BuyPrice,
                    Category = itemDto.Category,
                    Description = itemDto.Description,
                    IsHiring = itemDto.IsHiring
                };

                db.items.Add(item);
                await db.SaveChangesAsync();

                return Results.Created($"/items/{item.Id}", item);
            });

            app.MapGet("/itemsbydegree/{id}", async (int id, GownDb db) =>
            {
                var results = await db.itemDegreeModels
                    .FromSqlRaw(@"SELECT i.id, cd.degree_id as degree_id, NULL as degree_name, NULL as degree_order, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring
                    FROM public.ceremony_degree_item cdi
                    INNER JOIN public.items i on cdi.item_id = i.id
					INNER JOIN public.ceremony_degree cd on cdi.ceremony_degree_id = cd.id
                    WHERE cdi.ceremony_degree_id = {0}", id)
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
