using System.Threading.Tasks;
using GownApi.Model;

namespace GownApi.Services
{
    public interface IEmailService
    {
        Task SendContactEmailAsync(ContactRequest request);

        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}
