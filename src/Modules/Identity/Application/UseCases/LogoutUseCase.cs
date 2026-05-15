using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class LogoutUseCase(
    ISessionRepository sessionRepository,
    IDateTimeProvider clock)
{
    public async Task<Result> ExecuteAsync(string rawToken, CancellationToken ct = default)
    {
        var tokenHash = RegisterUseCase.HashToken(rawToken);
        var session = await sessionRepository.FindByTokenHashAsync(tokenHash, ct);

        if (session is null || session.RevokedAt is not null)
            return Result.Ok();

        session.Revoke(clock.UtcNow);
        await sessionRepository.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
