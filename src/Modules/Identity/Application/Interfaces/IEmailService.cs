namespace Helpdesk.Modules.Identity.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string rawToken, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken ct = default);
}
