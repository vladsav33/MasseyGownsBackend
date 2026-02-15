using GownApi.Model;
using GownApi.Model.Dto;
using GownApi.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class AdminItemsController
    {
        public static void AdminItemsEndpoints(this WebApplication app)
        {
            app.MapGet("/admin/items", async (GownDb db) => {
                var items = await db.items.Where(c => c.Category != "Donation" && c.Category != "Delivery")
                .OrderBy(c => c.Name).ToListAsync();

                var result = items.Select(i => new ItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Category = i.Category,
                    Description = i.Description,
                    HirePrice = i.HirePrice,
                    BuyPrice = i.BuyPrice,
                    IsHiring = i.IsHiring,
                    PictureBase64 = i.Picture != null
                    ? Convert.ToBase64String(i.Picture)
                    : null
                }).ToList();

                return result;
            });

            app.MapGet("/admin/itemsbydegree/{id}", async (int id, GownDb db) => {
                var results = await db.itemDegreeModels
                .FromSqlRaw(@"SELECT i.id, cd.degree_id as degree_id, NULL as degree_name, NULL as degree_order, i.name, i.picture, i.hire_price, i.buy_price, i.category, i.description, i.is_hiring, cdi.active
                                FROM public.ceremony_degree_item cdi
                                INNER JOIN public.items i on cdi.item_id = i.id
					            INNER JOIN public.ceremony_degree cd on cdi.ceremony_degree_id = cd.id
                                WHERE cd.degree_id = {0} AND cd.graduation_id = 2 ORDER BY i.category, i.name", id)
                .ToListAsync();

                var itemsDto = new List<ItemDegreeDto>();

                foreach (var i in results)
                {
                    var itemDto = ItemMapper.ToDto(i);
                    itemsDto.Add(itemDto);
                }

                var itemsDtoList = itemsDto.ToList();
                return Results.Ok(itemsDtoList);
            });

            app.MapPut("/admin/items/{id}", async (int id, ItemDto updatedItem, GownDb db, ILogger<Program> logger) =>
            {
                var jsonBody = JsonSerializer.Serialize(updatedItem, new JsonSerializerOptions
                {
                    WriteIndented = true // pretty-print JSON
                });

                logger.LogInformation("PUT /items/id called with ID={id}, Body={@updatedCeremony}", id, jsonBody);

                if (id != updatedItem.Id)
                    return Results.BadRequest("ID in URL and body must match");

                var item = await db.items.FindAsync(id);
                if (item is null)
                    return Results.NotFound();

                // Update fields
                item.Name = updatedItem.Name;
                if (updatedItem.PictureBase64 != null)
                {
                    //string base64 = updatedItem.PictureBase64.Split(',')[1];
                    item.Picture = Convert.FromBase64String(updatedItem.PictureBase64);
                }
                else
                {
                    item.Picture = null;
                }
                item.HirePrice = updatedItem.HirePrice;
                item.BuyPrice = updatedItem.BuyPrice;
                item.HirePrice = updatedItem.HirePrice;
                item.Category = updatedItem.Category;
                item.Description = updatedItem.Description;
                item.IsHiring = updatedItem.IsHiring;

                await db.SaveChangesAsync();

                return Results.Ok(updatedItem);
            });

            app.MapPost("/admin/items", async (ItemDto itemDto, GownDb db) =>
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

                var responseDto = new ItemDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    PictureBase64 = itemDto.PictureBase64,
                    HirePrice = item.HirePrice,
                    BuyPrice = item.BuyPrice,
                    Category = item.Category,
                    Description = item.Description,
                    IsHiring = item.IsHiring
                };

                return Results.Created($"/items/{item.Id}", responseDto);
            });

            app.MapPost("/admin/degrees/{degreeId}/items",
                async (int degreeId, List<ItemsUpdateDto> updates, GownDb db, ILogger<Program> logger) =>
            {
                logger.LogInformation(
                    "Updating items for DegreeId={DegreeId}. Items count={Count}",
                    degreeId,
                    updates?.Count ?? 0
                );

                if (updates == null || updates.Count == 0)
                {
                    logger.LogWarning("No updates provided for DegreeId={DegreeId}", degreeId);
                    return Results.BadRequest("No updates provided.");
                }

                foreach (var item in updates)
                {
                    string sql = @"
                        UPDATE ceremony_degree_item 
                        SET active = {2}
                        WHERE id IN (
                            SELECT cdi.id
                            FROM ceremony_degree_item cdi
                            INNER JOIN items i on cdi.item_id = i.id
                            INNER JOIN ceremony_degree cd on cdi.ceremony_degree_id = cd.id
                            WHERE cd.degree_id = {0} AND i.id = {1}
                        );
                    ";
                    int rows = await db.Database.ExecuteSqlRawAsync(sql, degreeId, item.Id, item.Active);

                    logger.LogInformation(
                        "Updated {Rows} ceremony_degree_item rows for DegreeId={DegreeId}, ItemId={Item.Id}, Status={Active}",
                        rows,
                        degreeId,
                        item.Id,
                        item.Active
                    );  
                }    

                return Results.Ok();
            });

            app.MapGet("/admin/items/ceremony/{id}", async (int id, GownDb db) => {

                var sql = @"SELECT o.id as id, o.first_name, o.last_name, 
                                STRING_AGG(s.gown_size::text, ', ') FILTER (WHERE i.category = 'Academic Gown')  AS gown_size,
                                STRING_AGG(s.stole_size, ', ') FILTER (WHERE i.category = 'Academic Gown') AS stole_size,
                                STRING_AGG(s.labelsize, ', ') FILTER (WHERE i.category = 'Headwear') AS hat_size,
                                STRING_AGG(h.short_name, ', ') FILTER (WHERE i.category = 'Hood') AS hood_name
                                FROM orders o
                                INNER JOIN ordered_items oi
                                ON oi.order_id = o.id
                                INNER JOIN sku sk
                                ON oi.sku_id = sk.id
                                INNER JOIN items i
                                ON i.id = sk.item_id
                                LEFT JOIN sizes s
                                ON sk.size_id = s.id
                                LEFT JOIN hood_type h
                                ON sk.hood_id = h.id
                                WHERE i.category <> 'Donation' AND ceremony_id = @id
                                GROUP BY o.id, o.first_name, o.last_name
                                ORDER BY o.id";

                var param = new NpgsqlParameter("@id", id);

                var itemsDetails = await db.itemDetails
                    .FromSqlRaw(sql, param)
                    //.AsNoTracking()
                    .ToListAsync();

                return Results.Ok(itemsDetails);
            });
        }
        public class ItemsUpdateDto
        {
            public int Id { get; set; }
            public bool Active { get; set; }
        }
    }
}
