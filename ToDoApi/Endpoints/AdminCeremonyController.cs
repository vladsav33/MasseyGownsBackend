using AutoMapper;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.Excel;
using GownApi.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GownApi.Endpoints
{

    public static class AdminCeremonyController
    {
        public static void AdminCeremonyEndpoints(this WebApplication app)
        {
            app.MapGet("/admin/ceremonies", async (GownDb db) => {
                var sql = @"
                    SELECT c.*,
	                COUNT(bo.hat_type) AS hat_count,
                    COUNT(bo.gown_type) AS gown_count,
                    COUNT(bo.hood_type) AS hood_count,
                    COUNT(bo.ucol_sash) AS ucol_count
                    FROM ceremonies c
                    LEFT JOIN bulk_orders bo
                    ON c.id = bo.ceremony_id
                    GROUP BY c.id";

                var result = await db.ceremonyDetails
                    .FromSqlRaw(sql)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return Results.Ok(result);
            });

            app.MapGet("admin/ceremonies/bulk/{id}", async (int id, GownDb db) =>
            {
                var sql = @"
                    SELECT COUNT(hat_type) AS hat_count,
                           COUNT(gown_type) AS gown_count,
                           COUNT(hood_type) AS hood_count,
                           COUNT(ucol_sash) AS ucol_count
                    FROM bulk_orders
                    WHERE ceremony_id = @id";

                var param = new NpgsqlParameter("@id", id);

                // Execute query and map to DTO
                var result = await db.countBulkDto
                    .FromSqlRaw(sql, param)
                    .AsNoTracking()
                    .FirstAsync();

                return Results.Ok(result);
            });

            app.MapPost("/admin/ceremonies", async (Ceremonies ceremony, GownDb db, ILogger<Program> logger) =>
                {
                    logger.LogInformation("POST /admin/ceremonies called with Body={@updatedCeremony}", ceremony);
                    try
                    {
                        db.ceremonies.Add(ceremony);
                        await db.SaveChangesAsync();

                        return Results.Created($"/ceremonies/{ceremony.Id}", ceremony);
                    } catch (DbUpdateException ex) when (
                        ex.InnerException is PostgresException pg &&
                        pg.SqlState == PostgresErrorCodes.UniqueViolation)
                    {
                        return Results.Conflict(new
                        {
                            message = "Ceremony code must be unique."
                        });
                    }
                });

            app.MapPut("/admin/ceremonies/{id}", async (int id, Ceremonies updatedCeremony, GownDb db, ILogger<Program> logger) =>
            {
                var jsonBody = JsonSerializer.Serialize(updatedCeremony, new JsonSerializerOptions
                {
                    WriteIndented = true // pretty-print JSON
                });

                logger.LogInformation("PUT /admin/ceremonies/id called with ID={id}, Body={@updatedCeremony}", id, jsonBody);

                if (id != updatedCeremony.Id)
                    return Results.BadRequest("ID in URL and body must match");

                var ceremony = await db.ceremonies.FindAsync(id);
                if (ceremony is null)
                    return Results.NotFound();

                var configExpression = new MapperConfigurationExpression();
                configExpression.CreateMap<Ceremonies, Ceremonies>()
                    .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

                var config = new MapperConfiguration(configExpression);
                IMapper mapper = config.CreateMapper();
                mapper.Map(updatedCeremony, ceremony);

                await db.SaveChangesAsync();
                return Results.Ok(ceremony);
            });
        }
    }
}
