using Helpdesk.Modules.Identity.Application.Security;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class LogoutUseCase(
    ISessionRepository sessionRepository,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    public async Task<Result> ExecuteAsync(string rawToken, CancellationToken ct = default)
    {
        var tokenHash = TokenHelper.HashToken(rawToken);
        var session = await sessionRepository.FindByTokenHashAsync(tokenHash, ct);

        if (session is null || session.RevokedAt is not null)
            return Result.Ok();

        var userId = session.UserId;
        session.Revoke(clock.UtcNow);
        await sessionRepository.SaveChangesAsync(ct);

        await auditService.RecordAsync("UserLoggedOut", "Identity", userId, userId,
            new { SessionId = session.Id }, ct);

        return Result.Ok();
    }
}
