using DocumentFormat.OpenXml.Office2010.Excel;
using GownApi.Model;
using GownApi.Model.Dto;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GownApi.Endpoints
{
    public static class AdminBulkOrderController
    {
        public static void AdminBulkOrderEndpoints(this WebApplication app)
        {
            app.MapGet("/admin/bulkorders", async (GownDb db) =>
            {
                return await db.bulkOrders.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync();

            });

            app.MapPost("/admin/bulkorders", async (List<BulkOrderDto> bulkOrdersDto, GownDb db, ILogger<Program> logger) =>
            {
                try
                {
                    var bulkOrder = new List<BulkOrder>();

                    logger.LogInformation(
                        "POST /admin/bulkorders payload:\n{Body}",
                        JsonSerializer.Serialize(
                            bulkOrdersDto,
                            new JsonSerializerOptions { WriteIndented = true }
                        )
                    );


                    foreach (var order in bulkOrdersDto)
                    {

                        logger.LogInformation("Ceremony Name={order.IDCode}", order.IDCode);

                        if (string.IsNullOrEmpty(order.IDCode))
                        {
                            return Results.NotFound("Ceremony name is null or missing");
                        };

                        // Find ceremony by name
                        var ceremony = await db.ceremonies
                            .FirstOrDefaultAsync(c => c.IdCode == order.IDCode);

                        if (ceremony == null)
                            return Results.NotFound($"Ceremony '{order.IDCode}' not found.");

                        logger.LogInformation("Ceremony ID={ceremony.Id}", ceremony.Id);

                        bulkOrder.Add(new BulkOrder
                        {
                            LastName = order.Name,
                            //FirstName = order.FirstName,
                            HeadSize = order.Headsize,
                            HatType = order.Hattype,
                            Height = order.Height,
                            GownType = order.Gowntype,
                            HoodType = order.Hood,
                            UcolSash = order.UcolSash,
                            CeremonyId = ceremony.Id
                        });
                    }



                    await db.bulkOrders.AddRangeAsync(bulkOrder);
                    await db.SaveChangesAsync();
                    return Results.Created("/admin/bulkorders", new { inserted = bulkOrdersDto.Count });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        //error = "One or more records violate database constraints",
                        error = ex.Message,
                        details = ex.InnerException?.Message
                    });
                }
            });
        }
    }
}
