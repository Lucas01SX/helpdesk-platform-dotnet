using Helpdesk.Modules.Identity.Application.Interfaces;

namespace Helpdesk.Tests.Integration.Infrastructure;

public sealed class InMemoryEmailService : IEmailService
{
    private readonly Dictionary<string, string> _verificationTokens = new();
    private readonly Dictionary<string, string> _resetTokens = new();

    public string? GetVerificationToken(string email) =>
        _verificationTokens.GetValueOrDefault(email);

    public string? GetResetToken(string email) =>
        _resetTokens.GetValueOrDefault(email);

    public Task SendEmailVerificationAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        _verificationTokens[toEmail] = rawToken;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        _resetTokens[toEmail] = rawToken;
        return Task.CompletedTask;
    }
}
