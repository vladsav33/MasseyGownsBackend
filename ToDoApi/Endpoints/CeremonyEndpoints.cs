using GownApi.Model;
using Microsoft.EntityFrameworkCore;
//using System.Collections.Specialized;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class CeremonyEndpoints
    {
        public static void MapCeremonyEndoints(this WebApplication app)
        {
            app.MapGet("/ceremonies", async (bool? all, GownDb db) => {
                if (all == true)
                    return await db.ceremonies.Where(c => !c.Name.Contains("Casual")).OrderBy(c => c.Name).ToListAsync();

                return await db.ceremonies.Where(c => c.Visible && !c.Name.Contains("Casual")).OrderBy(c => c.Name).ToListAsync();
            });

            app.MapGet("/ceremonies/visible-links", async (GownDb db) =>
            {
                var data = await db.ceremonies
                    .AsNoTracking()
                    .Where(c => c.Visible && c.Name != null && !c.Name.Contains("Casual"))
                    .OrderBy(c => c.Name)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name
                    })
                    .ToListAsync();

                return Results.Ok(data);
            });

            app.MapGet("/ceremonies/content/{id:int}", async (int id, GownDb db) =>
            {
                var ceremony = await db.ceremonies
                    .AsNoTracking()
                    .Where(c => c.Id == id && c.Visible)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Content,
                        c.CeremonyDate,
                        c.DueDate,
                        c.InstitutionName,
                        c.CourierAddress
                    })
                    .FirstOrDefaultAsync();

                if (ceremony == null)
                {
                    return Results.NotFound("Ceremony not found.");
                }

                return Results.Ok(ceremony);
            });

        }
    }
}
