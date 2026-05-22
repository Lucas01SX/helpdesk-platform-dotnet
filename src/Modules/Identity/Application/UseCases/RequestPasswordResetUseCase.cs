using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Application.Security;
using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class RequestPasswordResetUseCase(
    IUserRepository userRepository,
    IEmailService emailService,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    public async Task ExecuteAsync(string email, CancellationToken ct = default)
    {
        // Enumeration protection: always succeed silently, never reveal if email exists
        var user = await userRepository.FindByEmailAsync(email, ct);
        if (user is null) return;

        var now = clock.UtcNow;
        await userRepository.RevokePasswordResetTokensAsync(user.Id, ct);

        var rawToken = TokenHelper.GenerateSecureToken();
        var tokenHash = TokenHelper.HashToken(rawToken);
        var resetToken = PasswordResetToken.Create(user.Id, tokenHash, now);

        await userRepository.AddPasswordResetTokenAsync(resetToken, ct);
        await userRepository.SaveChangesAsync(ct);

        await emailService.SendPasswordResetAsync(user.Email, rawToken, ct);
        await auditService.RecordAsync("PasswordResetRequested", "Identity", user.Id, null,
            new { user.Email }, ct);
    }
}
