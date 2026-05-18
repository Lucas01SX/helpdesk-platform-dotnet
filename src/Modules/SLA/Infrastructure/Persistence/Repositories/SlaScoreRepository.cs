using Helpdesk.Modules.SLA.Domain.Entities;
using Helpdesk.Modules.SLA.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.SLA.Infrastructure.Persistence.Repositories;

internal sealed class SlaScoreRepository(DbContext context) : ISlaScoreRepository
{
    private readonly DbSet<SlaMonthlyScore> _scores = context.Set<SlaMonthlyScore>();

    public async Task<SlaMonthlyScore?> GetForMonthAsync(int year, int month, CancellationToken ct = default)
        => await _scores.FirstOrDefaultAsync(s => s.Year == year && s.Month == month, ct);

    public async Task<IReadOnlyList<SlaMonthlyScore>> GetHistoryAsync(int limit = 12, CancellationToken ct = default)
        => await _scores
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .Take(limit)
            .ToListAsync(ct);

    public async Task AddAsync(SlaMonthlyScore score, CancellationToken ct = default)
        => await _scores.AddAsync(score, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
