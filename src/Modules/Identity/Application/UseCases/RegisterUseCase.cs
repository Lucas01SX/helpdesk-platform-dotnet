using System.Security.Cryptography;
using System.Text;
using Helpdesk.Modules.Identity.Application.Contracts.Requests;
using Helpdesk.Modules.Identity.Application.Errors;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class RegisterUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailService emailService,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    public async Task<Result<Guid>> ExecuteAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
            return IdentityErrors.EmailAlreadyRegistered;

        var now = clock.UtcNow;
        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Create(request.Email, request.Name, passwordHash, UserRole.Customer, now);

        var rawToken = GenerateSecureToken();
        var tokenHash = HashToken(rawToken);
        var verificationToken = EmailVerificationToken.Create(user.Id, tokenHash, now);

        await userRepository.AddAsync(user, ct);
        await userRepository.AddEmailVerificationTokenAsync(verificationToken, ct);
        await userRepository.SaveChangesAsync(ct);

        await emailService.SendEmailVerificationAsync(user.Email, rawToken, ct);
        await auditService.RecordAsync("UserRegistered", "Identity", user.Id, null,
            new { user.Email, user.Name }, ct);

        return user.Id;
    }

    internal static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    internal static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
