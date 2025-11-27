using GownApi.Model;
using GownApi.Model.Dto;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class AdminDegreeController
    {
        public static void AdminDegreeEndpoints(this WebApplication app)
        {

            app.MapPost("/admin/degrees", async (Degrees degree, GownDb db) =>
            {
                db.degrees.Add(degree);
                await db.SaveChangesAsync();

                return Results.Created($"/degrees/{degree.Id}", degree);
            });

            app.MapPut("/admin/degrees/{id}", async (int id, Degrees updatedDegree, GownDb db) =>
            {
                if (id != updatedDegree.Id)
                    return Results.BadRequest("ID in URL and body must match");

                var degree = await db.degrees.FindAsync(id);
                if (degree is null)
                    return Results.NotFound();

                // Update fields
                degree.Name = updatedDegree.Name;

                await db.SaveChangesAsync();
                return Results.Ok(degree);
            });

        }
    }
}
