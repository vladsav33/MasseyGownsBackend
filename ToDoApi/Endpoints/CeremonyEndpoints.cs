using GownsApi;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class CeremonyEndpoints
    {
        public static void MapCeremonyEndoints(this WebApplication app)
        {
            app.MapGet("/ceremonies", async (GownDb db) =>
                await db.ceremonies.ToListAsync());

            app.MapPost("/ceremonies", async (Ceremonies ceremony, GownDb db) =>
            {
                db.ceremonies.Add(ceremony);
                await db.SaveChangesAsync();

                return Results.Created($"/ceremonies/{ceremony.Id}", ceremony);
            });
        }
    }
}
