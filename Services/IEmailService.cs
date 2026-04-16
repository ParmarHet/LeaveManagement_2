using Microsoft.AspNetCore.Identity.UI.Services;

namespace LeavePro.Services;

public interface IEmailService : IEmailSender
{
    Task SendEmailToAdminAsync(string subject, string htmlMessage);
}
