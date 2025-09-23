using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class ContactEndpoints
    {
        public static void MapContactEndpoints(this IEndpointRouteBuilder routes)
        {
            var group = routes.MapGroup("/contacts").WithTags("GownApi");

            // POST /contacts
            group.MapPost("/", async ([FromBody] Contacts contact, GownDb db) =>
            {
                if (contact == null)
                {
                    return Results.BadRequest("Contact data is required.");
                }

                contact.Id = $"contacts_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                contact.CreatedAt = DateTime.UtcNow;

                db.Contacts.Add(contact);
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Contact saved successfully!" });
            });

            // GET /contacts
            group.MapGet("/", async (GownDb db) =>
            {
                return await db.Contacts.ToListAsync();
            });

            // GET /contacts/{id}
            group.MapGet("/{id}", async (string id, GownDb db) =>
            {
                var contact = await db.Contacts.FindAsync(id);
                return contact is not null ? Results.Ok(contact) : Results.NotFound();
            });
        }
    }
}
