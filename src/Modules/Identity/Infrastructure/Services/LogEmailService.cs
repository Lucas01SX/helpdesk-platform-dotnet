using Helpdesk.Modules.Identity.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Modules.Identity.Infrastructure.Services;

internal sealed class LogEmailService(ILogger<LogEmailService> logger) : IEmailService
{
    public Task SendEmailVerificationAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        logger.LogInformation("[EMAIL] Verification email queued for {Email}", toEmail);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        logger.LogInformation("[EMAIL] Password reset email queued for {Email}", toEmail);
        return Task.CompletedTask;
    }
}
