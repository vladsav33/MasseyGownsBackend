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
        }
    }
}
