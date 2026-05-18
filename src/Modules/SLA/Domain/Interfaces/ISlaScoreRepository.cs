using Helpdesk.Modules.SLA.Domain.Entities;

namespace Helpdesk.Modules.SLA.Domain.Interfaces;

public interface ISlaScoreRepository
{
    Task<SlaMonthlyScore?> GetForMonthAsync(int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<SlaMonthlyScore>> GetHistoryAsync(int limit = 12, CancellationToken ct = default);
    Task AddAsync(SlaMonthlyScore score, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
