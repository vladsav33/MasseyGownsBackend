using AutoMapper;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.Excel;
using GownApi.Model;
using GownApi.Model.Dto;
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
                    SELECT c.*, p.hood, p.gown, p.hat, p.xtra_hood, p.ucol_sash,
	                COUNT(bo.hat_type) AS hat_count,
                    COUNT(bo.gown_type) AS gown_count,
                    COUNT(bo.hood_type) AS hood_count,
                    COUNT(bo.ucol_sash) AS ucol_count
                    FROM ceremonies c
                    LEFT JOIN bulk_orders bo
                    ON c.id = bo.ceremony_id
                    LEFT JOIN prices p
                    ON c.price_id = p.id
                    GROUP BY c.id, p.hood, p.gown, p.hat, p.xtra_hood, p.ucol_sash
                    ORDER BY c.name";

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

            app.MapPost("/admin/dataceremony", async (List<DataCeremonyDto> dataceremonyDto, GownDb db, ILogger<Program> logger) =>
            {
                try
                {
                    var dataceremony = new List<CeremonyImport>();

                    logger.LogInformation(
                        "POST /admin/dataceremony payload:\n{Body}",
                        JsonSerializer.Serialize(
                            dataceremonyDto,
                            new JsonSerializerOptions { WriteIndented = true }
                        )
                    );


                    foreach (var order in dataceremonyDto)
                    {

                        logger.LogInformation("Student Id={order.StudentId}", order.Student_ID);

                        if (order.Student_ID == 0)
                        {
                            return Results.NotFound("Student Id is null or missing");
                        }

                        dataceremony.Add(new CeremonyImport
                        {
                            Year = order.Year,
                            Location = order.Location,
                            CeremonyName = order.Ceremony_full_name1,
                            StudentId = order.Student_ID.ToString(),
                            Forename = order.Forename1,
                            Surname = order.Surname,
                            FullName = order.Full_Name,
                            ProgramCode = order.Programme_code,
                            ProgramDesc = order.Programme_Description,
                            Mobile = order.Mobile.ToString(),
                        });
                    }

                    await db.CeremonyImport.AddRangeAsync(dataceremony);
                    await db.SaveChangesAsync();
                    return Results.Created("/admin/dataceremony", new { inserted = dataceremonyDto.Count });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new
                    {
                        //error = "One or more records violate database constraints",
                        error = ex.Message,
                        details = ex.InnerException?.Message
                    });
                }
            });
        }
    }
}
