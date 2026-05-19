using Helpdesk.Modules.Identity.Application.Contracts.Responses;
using Helpdesk.Modules.Identity.Application.Errors;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class RefreshTokenUseCase(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

    public async Task<Result<AuthResponse>> ExecuteAsync(string rawToken, CancellationToken ct = default)
    {
        var tokenHash = RegisterUseCase.HashToken(rawToken);
        var session = await sessionRepository.FindByTokenHashAsync(tokenHash, ct);

        if (session is null)
            return IdentityErrors.InvalidOrExpiredToken;

        var now = clock.UtcNow;

        // Reuse detection: token already revoked → invalidate entire family
        if (session.RevokedAt is not null)
        {
            var familySessions = await sessionRepository.FindActiveFamilyAsync(session.FamilyId, now, ct);
            foreach (var active in familySessions)
                active.Revoke(now);
            await sessionRepository.SaveChangesAsync(ct);

            return IdentityErrors.SessionRevoked;
        }

        if (session.ExpiresAt <= now)
            return IdentityErrors.InvalidOrExpiredToken;

        var user = await userRepository.FindByIdAsync(session.UserId, ct);
        if (user is null)
            return IdentityErrors.InvalidOrExpiredToken;

        // Rotate: revoke current, issue new token in same family
        session.Revoke(now);

        var newRawToken = RegisterUseCase.GenerateSecureToken();
        var newTokenHash = RegisterUseCase.HashToken(newRawToken);

        var newSession = UserSession.Create(
            user.Id, session.FamilyId, newTokenHash,
            session.UserAgent, session.IpAddress,
            now, SessionLifetime);

        await sessionRepository.AddAsync(newSession, ct);
        await sessionRepository.SaveChangesAsync(ct);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role, newSession.Id);

        await auditService.RecordAsync("TokenRefreshed", "Identity", user.Id, user.Id,
            new { OldSessionId = session.Id, NewSessionId = newSession.Id }, ct);

        return new AuthResponse(accessToken, newRawToken, newSession.ExpiresAt);
    }
}
