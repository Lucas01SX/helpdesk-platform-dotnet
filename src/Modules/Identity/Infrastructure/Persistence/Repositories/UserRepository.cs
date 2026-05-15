using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(DbContext context) : IUserRepository
{
    private readonly DbSet<User> _users = context.Set<User>();
    private readonly DbSet<EmailVerificationToken> _emailTokens = context.Set<EmailVerificationToken>();
    private readonly DbSet<PasswordResetToken> _resetTokens = context.Set<PasswordResetToken>();

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await _users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant().Trim(), ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await _users.AnyAsync(u => u.Email == email.ToLowerInvariant().Trim(), ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _users.AddAsync(user, ct);

    public async Task AddEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken ct = default)
        => await _emailTokens.AddAsync(token, ct);

    public async Task<EmailVerificationToken?> FindEmailVerificationTokenByHashAsync(string hash, CancellationToken ct = default)
        => await _emailTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

    public async Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken ct = default)
        => await _resetTokens.AddAsync(token, ct);

    public async Task<PasswordResetToken?> FindPasswordResetTokenByHashAsync(string hash, CancellationToken ct = default)
        => await _resetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

    public async Task RevokePasswordResetTokensAsync(Guid userId, CancellationToken ct = default)
        => await _resetTokens
            .Where(t => t.UserId == userId && !t.Used)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Used, true), ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
