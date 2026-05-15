using Helpdesk.Modules.Identity.Domain.Entities;

namespace Helpdesk.Modules.Identity.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);

    Task AddEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken ct = default);
    Task<EmailVerificationToken?> FindEmailVerificationTokenByHashAsync(string hash, CancellationToken ct = default);

    Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken ct = default);
    Task<PasswordResetToken?> FindPasswordResetTokenByHashAsync(string hash, CancellationToken ct = default);
    Task RevokePasswordResetTokensAsync(Guid userId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
