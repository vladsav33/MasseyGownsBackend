using GownApi.Model;
using GownApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Text.Json;

namespace GownApi.Endpoints
{
    public static class AdminHoodController
    {
        public static void AdminHoodEndpoints(this WebApplication app)
        {
            app.MapGet("/admin/hoods/{id}", async (int id, GownDb db) =>
            {
                return await db.hood_type.Where(c => c.ItemId == id).OrderBy(c => c.Name).ToListAsync();
            });

            app.MapPut("/admin/hoods/{id}", async (int id, HoodType updated, GownDb db) =>
            {
                var hood = await db.hood_type.FindAsync(updated.Id);
                if (hood is null)
                    return Results.NotFound();

                hood.Name = updated.Name;
                await db.SaveChangesAsync();
                return Results.Ok(hood);
            });

            app.MapPost("/admin/hoods", async (HoodType newHood, GownDb db) =>
            {
                db.hoods.Add(newHood);
                await db.SaveChangesAsync();
                return Results.Created("Created /admin/hoods", newHood);
            });
        }
    }
}
