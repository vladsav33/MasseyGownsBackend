using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using GownApi.Model;

namespace GownApi.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(EmailSettings settings)
        {
            _settings = settings;
        }

        // ----------------------------
        // 1) Generic email sender
        // ----------------------------
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
            };

            var fromAddress = new MailAddress(_settings.From, "ADH Website");
            var toAddress = new MailAddress(toEmail);

            using var mail = new MailMessage
            {
                From = fromAddress,
                Subject = subject,
                Body = body
            };

            mail.To.Add(toAddress);

            await client.SendMailAsync(mail);
        }


        // ----------------------------
        // 2) Contact form email sender
        // ----------------------------
        public async Task SendContactEmailAsync(ContactRequest request)
        {
            var toAddress = string.IsNullOrWhiteSpace(request.ToEmail)
                ? _settings.To
                : request.ToEmail;

            var subject = string.IsNullOrWhiteSpace(request.Subject)
                ? "New enquiry from the ADH website"
                : request.Subject;

            var body = BuildBody(request);

            await SendEmailAsync(toAddress, subject, body);

            // Also set reply-to if user provided an email
            // (Recreate message so it includes ReplyTo)
        }


        private static string BuildBody(ContactRequest r)
        {
            var sb = new StringBuilder();

            sb.AppendLine("A new enquiry has been submitted from the ADH website.");
            sb.AppendLine();
            sb.AppendLine($"First name: {r.FirstName}");
            sb.AppendLine($"Last name: {r.LastName}");
            sb.AppendLine($"Email: {r.Email}");
            sb.AppendLine();
            sb.AppendLine("Message:");
            sb.AppendLine(r.Enquiry ?? string.Empty);

            return sb.ToString();
        }
    }
}
