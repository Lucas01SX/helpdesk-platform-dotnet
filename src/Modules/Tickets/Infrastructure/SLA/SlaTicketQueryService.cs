using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Tickets.Infrastructure.SLA;

internal sealed class SlaTicketQueryService(DbContext context) : ISlaTicketQueryService
{
    private readonly DbSet<Ticket> _tickets = context.Set<Ticket>();

    public async Task<IReadOnlyList<SlaTicketView>> GetForFinalScoringAsync(CancellationToken ct = default)
        => await _tickets.AsNoTracking()
            .Where(t => !t.SlaScoreApplied &&
                        (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Cancelled))
            .Select(t => new SlaTicketView(
                t.Id, t.Status.ToString(), t.SlaDueAt, t.UpdatedAt,
                t.SlaBreachedAt, t.AssigneeId, t.AutoAssignedAt,
                t.SlaScoreApplied, t.SlaExcluded, t.SlaUnassignedPenaltyCount))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlaTicketView>> GetBreachedActiveAsync(DateTime now, CancellationToken ct = default)
        => await _tickets.AsNoTracking()
            .Where(t => t.SlaBreachedAt == null &&
                        t.Status != TicketStatus.Resolved &&
                        t.Status != TicketStatus.Cancelled &&
                        t.SlaDueAt < now)
            .Select(t => new SlaTicketView(
                t.Id, t.Status.ToString(), t.SlaDueAt, t.UpdatedAt,
                t.SlaBreachedAt, t.AssigneeId, t.AutoAssignedAt,
                t.SlaScoreApplied, t.SlaExcluded, t.SlaUnassignedPenaltyCount))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlaTicketView>> GetUnassignedBreachedAsync(CancellationToken ct = default)
        => await _tickets.AsNoTracking()
            .Where(t => t.SlaBreachedAt != null &&
                        t.AssigneeId == null &&
                        t.Status != TicketStatus.Resolved &&
                        t.Status != TicketStatus.Cancelled)
            .Select(t => new SlaTicketView(
                t.Id, t.Status.ToString(), t.SlaDueAt, t.UpdatedAt,
                t.SlaBreachedAt, t.AssigneeId, t.AutoAssignedAt,
                t.SlaScoreApplied, t.SlaExcluded, t.SlaUnassignedPenaltyCount))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlaTicketView>> GetCandidatesForAutoAssignAsync(CancellationToken ct = default)
        => await _tickets.AsNoTracking()
            .Where(t => t.SlaBreachedAt != null &&
                        t.AssigneeId == null &&
                        t.AutoAssignedAt == null &&
                        t.Status != TicketStatus.Resolved &&
                        t.Status != TicketStatus.Cancelled)
            .Select(t => new SlaTicketView(
                t.Id, t.Status.ToString(), t.SlaDueAt, t.UpdatedAt,
                t.SlaBreachedAt, t.AssigneeId, t.AutoAssignedAt,
                t.SlaScoreApplied, t.SlaExcluded, t.SlaUnassignedPenaltyCount))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SlaTicketView>> GetTimedOutAsync(DateTime cutoff, CancellationToken ct = default)
        => await _tickets.AsNoTracking()
            .Where(t => t.AutoAssignedAt != null &&
                        t.AutoAssignedAt <= cutoff &&
                        t.Status != TicketStatus.Resolved &&
                        t.Status != TicketStatus.Cancelled)
            .Select(t => new SlaTicketView(
                t.Id, t.Status.ToString(), t.SlaDueAt, t.UpdatedAt,
                t.SlaBreachedAt, t.AssigneeId, t.AutoAssignedAt,
                t.SlaScoreApplied, t.SlaExcluded, t.SlaUnassignedPenaltyCount))
            .ToListAsync(ct);
}
