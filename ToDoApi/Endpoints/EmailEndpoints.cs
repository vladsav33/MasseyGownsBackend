using GownApi.Model;
using GownApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GownApi.Endpoints
{
    public static class EmailEndpoints
    {
        public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/email/send", async (
                ContactRequest request,
                IEmailService emailService) =>
            {
                if (string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.FirstName) ||
                    string.IsNullOrWhiteSpace(request.Enquiry))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Email, first name and enquiry are required."
                    });
                }


                try
                {
                    await emailService.SendContactEmailAsync(request);

                    return Results.Ok(new
                    {
                        success = true,
                        message = "Email sent successfully."
                    });
                }
                catch (Exception ex)
                {
                    // TODO: log ex if needed

                    return Results.Json(
         new
         {
             success = false,
             message = "Failed to send email.",
             detail = ex.Message
         },
         statusCode: StatusCodes.Status500InternalServerError
     );
                }
            });

            return app;
        }
    }
}
