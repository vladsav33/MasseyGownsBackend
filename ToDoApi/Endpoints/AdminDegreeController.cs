using DocumentFormat.OpenXml.Office2010.Excel;
using GownApi.Model;
using GownApi.Model.Dto;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GownApi.Endpoints
{
    public static class AdminDegreeController
    {
        public static void AdminDegreeEndpoints(this WebApplication app)
        {
            app.MapGet("/admin/degreesbyceremony/{id}", async (int id, GownDb db) =>
            {
                if (id != 0) {
                    var results = await db.degreesCeremonies
                        .FromSqlRaw(@"SELECT g.id, c.ceremony_date, c.name ceremony_name, d.id as degree_id, d.name degree_name, g.active
                                FROM public.ceremony_degree g
                                INNER JOIN public.ceremonies c ON c.id = g.graduation_id
                                INNER JOIN public.degrees d ON d.id = g.degree_id
                                WHERE c.id = {0} ORDER BY d.degree_order", id)
                        .ToListAsync();

                    return Results.Ok(results);
                } else {
                    var results = await db.degreesCeremonies
                        .FromSqlRaw(@"SELECT d.id, NULL as ceremony_date, '' as ceremony_name, d.id as degree_id, d.name degree_name, TRUE as active
                                FROM public.degrees d")
                        .ToListAsync();

                    return Results.Ok(results);
                }
            });

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

            app.MapPost("/admin/ceremonies/{ceremonyId}/degrees",
                async (int ceremonyId, List<DegreeUpdateDto> updates, GownDb db, ILogger<Program> logger) =>
            {
                if (updates == null || updates.Count == 0)
                    return Results.BadRequest("No updates provided.");

                // Load existing many-to-many rows
                var existing = await db.ceremonyDegree
                    .Where(cd => cd.GraduationId == ceremonyId)
                    .ToListAsync();

                foreach (var update in updates)
                {
                    logger.LogInformation("DegreeId={degreeId} JsonBody={@updates}", update.DegreeId, update);
                    var record = existing.FirstOrDefault(cd => cd.DegreeId == update.DegreeId);

                    if (record != null)
                    {
                        logger.LogInformation("POST /admin/ceremonies/{ceremonyid}/degrees called with ID={ceremonyId}, Body={@updates}", ceremonyId, ceremonyId, update);
                        // Update existing row
                        record.Active = update.Active ?? false;
                    }
                    else
                    {
                        // Insert new row
                        db.ceremonyDegree.Add(new CeremonyDegree
                        {
                            GraduationId = ceremonyId,
                            DegreeId = update.DegreeId,
                            Active = update.Active ?? false
                        });
                    }
                }

                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Degrees updated successfully" });
            });
        }

        public class DegreeUpdateDto
        {
            public int DegreeId { get; set; }
            public bool? Active { get; set; }
        }
    }
}
