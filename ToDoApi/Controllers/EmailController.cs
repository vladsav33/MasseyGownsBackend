using GownApi.Model.Dto;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;

namespace GownApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        [HttpPost("send")]
        public IActionResult SendEmail([FromBody] EmailRequest request)
        {
            try
            {
                var smtpClient = new SmtpClient("sandbox.smtp.mailtrap.io")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("19963bc3a676ba", "5a81f70c91becf"),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(request.From ?? "noreply@yourdomain.com"),
                    Subject = request.Subject,
                    Body = request.Body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(request.To);

                smtpClient.Send(mailMessage);

                return Ok(new { message = "Email sent successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
