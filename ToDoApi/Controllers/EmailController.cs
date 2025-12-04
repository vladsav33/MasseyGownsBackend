using GownApi.Model.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace GownApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly SmtpSettings _smtp;

        public EmailController(IOptions<SmtpSettings> options)
        {
            _smtp = options.Value;
        }

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

        [HttpPost("sendOrderEmail")]
        public async Task<IActionResult> SendOrderEmail([FromBody] EmailRequest req)
        {
            if (req == null)
                return BadRequest("Invalid email payload.");

            if (string.IsNullOrWhiteSpace(req.To))
                return BadRequest(new { error = "The 'to' field is required." });

            if (string.IsNullOrWhiteSpace(req.Subject))
                return BadRequest(new { error = "The 'subject' field is required." });

            if (string.IsNullOrWhiteSpace(req.HtmlBody))
                return BadRequest(new { error = "The 'htmlBody' field is required." });

            var host = _smtp.Host;
            var port = _smtp.Port;
            var username = _smtp.Username;
            var password = _smtp.Password;

            using var smtp = new SmtpClient(host)
            {
                Port = (int)port,
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password)
            };

            var mail = new MailMessage
            {
                From = new MailAddress("info@masseygowns.org.nz"),
                Subject = req.Subject,
                Body = req.HtmlBody,
                IsBodyHtml = true
            };

            mail.To.Add(req.To);

            await smtp.SendMailAsync(mail);

            return Ok(new { success = true });
        }
    }
}
