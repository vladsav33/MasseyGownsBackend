using GownApi.Model;
using GownApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
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

                var hasher = new PasswordHasher<User>();
                string hash = hasher.HashPassword(user, user.PasswordHash);

                user.PasswordHash = hash;
                db.users.Add(user);

                try {
                    await db.SaveChangesAsync();
                } catch(DbUpdateException ex) when (ex.InnerException is PostgresException pg) {
                    return Results.BadRequest(PgError.Match(pg));
                }
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

            app.MapPost("/api/auth/check-password", async (
                CheckPasswordRequest request,
                GownDb db) =>
            {
                var user = await db.users.SingleOrDefaultAsync(x => x.Name == request.Username && x.Active == true);
                if (user == null)
                    return Results.NotFound("User not found");

                var hasher = new PasswordHasher<User>();
                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

                if (result == PasswordVerificationResult.Success)
                    return Results.Ok(new { valid = true });

                return Results.Ok(new { valid = false });
            });

            app.MapPut("/api/users/{id}/change-password", async (
                int id,
                ChangePasswordRequest req,
                GownDb db ) => {
                if (string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest("Password is required.");

                var user = await db.users.FindAsync(id);
                if (user == null)
                    return Results.NotFound("User not found.");

                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, req.Password);

                await db.SaveChangesAsync();

                return Results.Ok("Password updated.");
            });

            app.MapPut("/admin/users/{id}/active", async (
                int id,
                ChangeActive req,
                GownDb db) => {
                if (req.Active == null)
                    return Results.BadRequest("Active flag is required.");

                var user = await db.users.FindAsync(id);
                if (user == null)
                    return Results.NotFound("User not found.");

                user.Active = req.Active;

                await db.SaveChangesAsync();

                return Results.Ok("Active status updated.");
            });

            app.MapDelete("/admin/users/{id}", async (int id, GownDb db) =>
            {
                var user = await db.users.FindAsync(id);

                if (user == null)
                    return Results.NotFound("User not found.");

                db.users.Remove(user);
                await db.SaveChangesAsync();

                return Results.Ok($"User {id} deleted.");
            });
        }
    }
    public class ChangePasswordRequest
    {
        public string Password { get; set; }
    }

    public class ChangeActive
    {
        public bool? Active { get; set; }
    }
}

