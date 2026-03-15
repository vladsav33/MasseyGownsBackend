using DocumentFormat.OpenXml.Bibliography;
using GownApi.Services;
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
            group.MapPost("/", async ([FromBody] Contacts contact, GownDb db, IQueueJobPublisher publisher, ILogger<Program> logger) =>
            {
                if (contact == null)
                {
                    return Results.BadRequest("Contact data is required.");
                }

                contact.Id = $"contacts_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var nzTz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
                //contact.CreatedAt = TimeZoneInfo.ConvertTime(DateTime.UtcNow, nzTz);
                contact.CreatedAt = DateTime.UtcNow;

                db.Contacts.Add(contact);
                await db.SaveChangesAsync();

                try
                {
                    await publisher.EnqueueEmailJobAsync(new EmailJob(
                      Type: "ContactQuery",
                      OrderId: null,
                      ReferenceNo: contact.Subject,
                      TxnId: contact.Query,
                      OccurredAt: TimeZoneInfo.ConvertTime(contact.CreatedAt, nzTz),
                      EmailQueueItemId: null
                  ));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send Contact Query email for subject:{subject}", contact.Subject);
                }

                return Results.Ok(new { message = "Contact saved successfully!" });
            });

            // GET /contacts
            group.MapGet("/", async (GownDb db, ILogger<Program> logger) =>
            {
                try
                {
                    logger.LogInformation("GET /contacts started");

                    var contacts = await db.Contacts.ToListAsync();

                    logger.LogInformation("GET /contacts succeeded. Count: {Count}", contacts.Count);

                    return Results.Ok(contacts);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GET /contacts failed");
                    return Results.Problem(ex.Message);
                }
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
