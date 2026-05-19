using System.Security.Cryptography;
using System.Text;
using Helpdesk.Modules.Identity.Application.Contracts.Requests;
using Helpdesk.Modules.Identity.Application.Contracts.Responses;
using Helpdesk.Modules.Identity.Application.Errors;
using Helpdesk.Modules.Identity.Application.Interfaces;
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

    // Valid-format Argon2id placeholder (base64(16-byte salt).base64(32-byte hash)).
    // Used when the email does not exist so Argon2id always runs, keeping response
    // time constant and preventing timing-based email enumeration.
    private const string DummyHash =
        "AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public async Task<Result<AuthResponse>> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.FindByEmailAsync(request.Email, ct);

        var hashToVerify = user?.PasswordHash ?? DummyHash;
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

        var rawToken = RegisterUseCase.GenerateSecureToken();
        var tokenHash = RegisterUseCase.HashToken(rawToken);
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
