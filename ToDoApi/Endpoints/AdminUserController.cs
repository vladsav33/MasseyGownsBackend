using GownApi.Model;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class AdminUserController
    {
        public static void AdminUserEndpoints(this WebApplication app)
        {
            app.MapGet("/admin/users", async (GownDb db) => {
                return await db.users.OrderBy(c => c.Name).ToListAsync();
            });

            app.MapPost("/admin/users", async (User user, GownDb db) =>
            {
                db.users.Add(user);
                await db.SaveChangesAsync();

                return Results.Created($"/admin/users/{user.Id}", user);
            });

            app.MapPut("/admin/users/{id}", async (int id, User updatedUser, GownDb db, ILogger<Program> logger) =>
            {
                var jsonBody = JsonSerializer.Serialize(updatedUser, new JsonSerializerOptions
                {
                    WriteIndented = true // pretty-print JSON
                });

                logger.LogInformation("PUT /admin/users/id called with ID={id}, Body={@updatedUser}", id, jsonBody);

                if (id != updatedUser.Id)
                    return Results.BadRequest("ID in URL and body must match");

                var user = await db.users.FindAsync(id);
                if (user is null)
                    return Results.NotFound();

                // Update fields
                user.Name = updatedUser.Name;
                user.Email = updatedUser.Email;
                user.PasswordHash = updatedUser.PasswordHash;
                user.Active = updatedUser.Active;

                await db.SaveChangesAsync();

                return Results.Ok(user);
            });
        }
    }
}

