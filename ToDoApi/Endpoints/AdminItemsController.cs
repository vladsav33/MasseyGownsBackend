using GownApi.Model.Dto;
using Microsoft.EntityFrameworkCore;
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
        }
    }
}
