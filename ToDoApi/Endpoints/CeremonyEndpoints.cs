﻿using GownApi.Model;
using Microsoft.EntityFrameworkCore;
//using System.Collections.Specialized;
using System.Text.Json;

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

            app.MapPut("/ceremonies/{id}", async (int id, Ceremonies updatedCeremony, GownDb db, ILogger<Program> logger) =>
            {
                var jsonBody = JsonSerializer.Serialize(updatedCeremony, new JsonSerializerOptions
                {
                    WriteIndented = true // pretty-print JSON
                });

                logger.LogInformation("PUT /ceremonies/id called with ID={id}, Body={@updatedCeremony}", id, jsonBody);

                if (id != updatedCeremony.Id)
                    return Results.BadRequest("ID in URL and body must match");

                var ceremony = await db.ceremonies.FindAsync(id);
                if (ceremony is null)
                    return Results.NotFound();

                // Update fields
                ceremony.Name = updatedCeremony.Name;
                ceremony.DueDate = updatedCeremony.DueDate;

                await db.SaveChangesAsync();

                return Results.Ok(ceremony);
            });
        }
    }
}
