using Helpdesk.Modules.Identity.Application.Contracts.Requests;
using Helpdesk.Modules.Identity.Application.Contracts.Responses;
using Helpdesk.Modules.Identity.Application.Errors;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Application.Security;
using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class LoginUseCase(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
    private const int MaxActiveSessions = 5;

    public async Task<Result<AuthResponse>> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.FindByEmailAsync(request.Email, ct);

        // Run Argon2 even when the email doesn't exist to keep response time constant
        // and prevent timing-based email enumeration. GetDummyHash() returns a valid
        // hash in the same format as stored hashes, so Verify() always runs the full KDF.
        var hashToVerify = user?.PasswordHash ?? passwordHasher.GetDummyHash();
        var passwordValid = passwordHasher.Verify(request.Password, hashToVerify);

        if (user is null || !passwordValid)
            return IdentityErrors.InvalidCredentials;

        if (!user.EmailVerified)
            return IdentityErrors.EmailNotVerified;

        var now = clock.UtcNow;

        // Revoke oldest session when max is reached
        var activeCount = await sessionRepository.CountActiveAsync(user.Id, now, ct);
        if (activeCount >= MaxActiveSessions)
        {
            var oldest = await sessionRepository.FindOldestActiveAsync(user.Id, now, ct);
            if (oldest is not null)
            {
                oldest.Revoke(now);
                await sessionRepository.SaveChangesAsync(ct);
            }
        }

        var rawToken = TokenHelper.GenerateSecureToken();
        var tokenHash = TokenHelper.HashToken(rawToken);
        var familyId = Guid.NewGuid();

        var session = UserSession.Create(
            user.Id, familyId, tokenHash,
            request.UserAgent, request.IpAddress,
            now, SessionLifetime);

        await sessionRepository.AddAsync(session, ct);
        await sessionRepository.SaveChangesAsync(ct);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role, session.Id);

        await auditService.RecordAsync("UserLoggedIn", "Identity", user.Id, user.Id,
            new { user.Email, SessionId = session.Id }, ct);

        return new AuthResponse(accessToken, rawToken, session.ExpiresAt);
    }
}
