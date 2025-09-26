using GownApi.Model;
using Microsoft.EntityFrameworkCore;

namespace GownApi.Endpoints
{
    public static class FaqEndpoints
    {
        public static void MapFaqEndpoints(this WebApplication app)
        {
            app.MapGet("/faq", async (GownDb db) =>
                await db.faq.ToListAsync());

            app.MapPost("/faq", async (Faq faq, GownDb db) =>
            {
                db.faq.Add(faq);
                await db.SaveChangesAsync();

                return Results.Created($"/faq/{faq.Id}", faq);
            });

            app.MapDelete("/faq/{id}", async (int id, GownDb db) =>
            {
                if (await db.faq.FindAsync(id) is Faq faq)
                {
                    db.faq.Remove(faq);
                    await db.SaveChangesAsync();
                    return Results.NoContent();
                }

                return Results.NotFound();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

            app.MapPut("/faq/{id}", async (int id, Faq faqIn, GownDb db) =>
            {
                var faq = await db.faq.FindAsync(id);

                if (faq is null) return Results.NotFound();

                faq.Question = faqIn.Question;
                faq.Answer = faqIn.Answer;
                faq.Category = faqIn.Category;

                await db.SaveChangesAsync();

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
