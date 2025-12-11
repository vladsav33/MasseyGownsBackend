using AutoMapper;
using GownApi.Model;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GownApi.Endpoints
{

    public static class AdminCeremonyController
    {
        public static void AdminCeremonyEndpoints(this WebApplication app)
        {

            app.MapGet("/admin/ceremonies", async (GownDb db) => {
                return await db.ceremonies.OrderBy(c => c.Name).ToListAsync();

            });

            app.MapPost("/admin/ceremonies", async (Ceremonies ceremony, GownDb db) =>
                {
                    db.ceremonies.Add(ceremony);
                    await db.SaveChangesAsync();

                    return Results.Created($"/ceremonies/{ceremony.Id}", ceremony);
                });

            app.MapPut("/admin/ceremonies/{id}", async (int id, Ceremonies updatedCeremony, GownDb db, ILogger<Program> logger) =>
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

                var configExpression = new MapperConfigurationExpression();
                configExpression.CreateMap<Ceremonies, Ceremonies>()
                    .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

                var config = new MapperConfiguration(configExpression);
                IMapper mapper = config.CreateMapper();
                mapper.Map(updatedCeremony, ceremony);


                //// Update fields
                //ceremony.Name = updatedCeremony.Name;
                //ceremony.CeremonyDate = updatedCeremony.CeremonyDate;
                //ceremony.DueDate = updatedCeremony.DueDate;
                //ceremony.Visible = updatedCeremony.Visible;
                //ceremony.IdCode = updatedCeremony.IdCode;
                //ceremony.InstitutionName = updatedCeremony.InstitutionName;
                //ceremony.City = updatedCeremony.City;
                //ceremony.CourierAddress = updatedCeremony.CourierAddress;
                //ceremony.PostalAddress = updatedCeremony.PostalAddress;
                //ceremony.DespatchDate = updatedCeremony.DespatchDate;
                //ceremony.DateSent = updatedCeremony.DateSent;
                //ceremony.ReturnDate = updatedCeremony.ReturnDate;
                //ceremony.DateReturned = updatedCeremony.DateReturned;
                //ceremony.Organiser = updatedCeremony.Organiser;
                //ceremony.Phone = updatedCeremony.Phone;
                //ceremony.Email = updatedCeremony.Email;
                //ceremony.InvoiceEmail = updatedCeremony.InvoiceEmail;
                //ceremony.PriceCode = updatedCeremony.PriceCode;
                //ceremony.Freight = updatedCeremony.Freight;

                await db.SaveChangesAsync();

                return Results.Ok(ceremony);
            });
        }
    }
}
