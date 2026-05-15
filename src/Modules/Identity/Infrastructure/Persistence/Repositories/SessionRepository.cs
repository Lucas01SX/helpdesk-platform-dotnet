using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class SessionRepository(DbContext context) : ISessionRepository
{
    private readonly DbSet<UserSession> _sessions = context.Set<UserSession>();

    public async Task<UserSession?> FindByTokenHashAsync(string hash, CancellationToken ct = default)
        => await _sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, ct);

    public async Task<IReadOnlyList<UserSession>> FindActiveFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default)
        => await _sessions
            .Where(s => s.FamilyId == familyId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserSession>> FindAllActiveByUserIdAsync(Guid userId, DateTime now, CancellationToken ct = default)
        => await _sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(ct);

    public async Task<int> CountActiveAsync(Guid userId, DateTime now, CancellationToken ct = default)
        => await _sessions
            .CountAsync(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now, ct);

    public async Task<UserSession?> FindOldestActiveAsync(Guid userId, DateTime now, CancellationToken ct = default)
        => await _sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(UserSession session, CancellationToken ct = default)
        => await _sessions.AddAsync(session, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
