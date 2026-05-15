using Helpdesk.Modules.Identity.Application.Errors;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Identity.Application.UseCases;

public sealed class VerifyEmailUseCase(
    IUserRepository userRepository,
    IDateTimeProvider clock)
{
    public async Task<Result> ExecuteAsync(string rawToken, CancellationToken ct = default)
    {
        var tokenHash = RegisterUseCase.HashToken(rawToken);
        var token = await userRepository.FindEmailVerificationTokenByHashAsync(tokenHash, ct);

        var now = clock.UtcNow;
        if (token is null || !token.IsValid(now))
            return IdentityErrors.InvalidOrExpiredToken;

        var user = await userRepository.FindByIdAsync(token.UserId, ct);
        if (user is null)
            return IdentityErrors.InvalidOrExpiredToken;

        token.MarkAsUsed();
        user.VerifyEmail(now);
        await userRepository.SaveChangesAsync(ct);

        return Result.Ok();
    }
}
