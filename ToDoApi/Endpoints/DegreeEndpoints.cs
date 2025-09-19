using GownsApi;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class DegreeEndpoints
    {
        public static void MapDegreeEndpoints(this WebApplication app)
        {
            app.MapGet("/degrees/{id}", async (int id, GownDb db) =>
                await db.degrees.FindAsync(id)
                 is Degrees degrees
                ? Results.Ok(degrees)
                : Results.NotFound());

            app.MapGet("/degrees", async (GownDb db) =>
                await db.degrees.ToListAsync());

            app.MapPost("/degrees", async (Degrees degree, GownDb db) =>
            {
                db.degrees.Add(degree);
                await db.SaveChangesAsync();

                return Results.Created($"/degrees/{degree.Id}", degree);
            });

            app.MapGet("/degreesbyceremony/{id}", async (int id, GownDb db) =>
            {
                var results = await db.degreesCeremonies
                    .FromSqlRaw(@"SELECT g.id, c.ceremony_date, c.name ceremony_name, d.name degree_name
                            FROM public.ceremony_degree g
                            INNER JOIN public.ceremonies c ON c.id = g.graduation_id
                            INNER JOIN public.degrees d ON d.id = g.degree_id
                            WHERE c.id = {0}", id)
                    .ToListAsync();

                return Results.Ok(results);
            });

            app.MapGet("/degreesandceremonies", async (GownDb db) =>
            {
                var menu = new List<MenuItem>();
                var results = await db.ceremonies.ToListAsync();

                foreach (var result in results)
                {
                    var degrees = await db.degreesCeremonies
                        .FromSqlRaw(@"SELECT g.id, c.ceremony_date, c.name ceremony_name, d.name degree_name
                            FROM public.ceremony_degree g
                            INNER JOIN public.ceremonies c ON c.id = g.graduation_id
                            INNER JOIN public.degrees d ON d.id = g.degree_id
                            WHERE c.id = {0}", result.Id)
                        .ToListAsync();
                    var children = new List<MenuItem>();
                    foreach (var degree in degrees)
                    {
                        children.Add(new MenuItem { Id = degree.Id, Name = degree.DegreeName });
                    }
                    menu.Add(new MenuItem { Id = result.Id, Name = result.Name, Children = children });
                }

                return Results.Ok(menu);
            });
        }
    }
}
