using Helpdesk.Modules.Identity.Application.Contracts.Requests;
using Helpdesk.Modules.Identity.Application.Errors;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class ResetPasswordUseCase(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    public async Task<Result> ExecuteAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var tokenHash = RegisterUseCase.HashToken(request.Token);
        var resetToken = await userRepository.FindPasswordResetTokenByHashAsync(tokenHash, ct);

        var now = clock.UtcNow;
        if (resetToken is null || !resetToken.IsValid(now))
            return IdentityErrors.InvalidOrExpiredToken;

        var user = await userRepository.FindByIdAsync(resetToken.UserId, ct);
        if (user is null)
            return IdentityErrors.InvalidOrExpiredToken;

        resetToken.MarkAsUsed();
        user.UpdatePassword(passwordHasher.Hash(request.NewPassword), now);

        // Revoke all active sessions to force re-login after password reset
        var activeSessions = await sessionRepository.FindAllActiveByUserIdAsync(user.Id, now, ct);
        foreach (var session in activeSessions)
            session.Revoke(now);

        await userRepository.SaveChangesAsync(ct);
        await sessionRepository.SaveChangesAsync(ct);

        await auditService.RecordAsync("PasswordResetApplied", "Identity", user.Id, user.Id,
            new { user.Email }, ct);

        return Result.Ok();
    }
}
