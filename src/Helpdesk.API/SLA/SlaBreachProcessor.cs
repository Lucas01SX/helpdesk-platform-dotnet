using Helpdesk.API.Persistence;
using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Modules.SLA.Domain.Entities;
using Helpdesk.Modules.SLA.Domain.Interfaces;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.SLA;

public sealed class SlaBreachProcessor(
    AppDbContext db,
    ISlaScoreRepository scoreRepository,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    private const int AutoCancelHoursAfterAutoAssign = 10;
    private const string AutoCancelReason =
        "No resolution after 10 hours. Please reopen with High priority.";

    public async Task ProcessAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        await ApplyFinalScoresAsync(now, ct);
        await DetectNewBreachesAsync(now, ct);
        await ApplyUnassignedPenaltiesAsync(now, ct);
        await AutoAssignBreachedTicketsAsync(now, ct);
        await AutoCancelTimedOutTicketsAsync(now, ct);
    }

    // 1 — Score tickets that reached a final state since last processing run
    private async Task ApplyFinalScoresAsync(DateTime now, CancellationToken ct)
    {
        var finalTickets = await db.Set<Ticket>()
            .Where(t =>
                !t.SlaScoreApplied &&
                (t.Status == TicketStatus.Resolved ||
                 t.Status == TicketStatus.Cancelled))
            .ToListAsync(ct);

        if (finalTickets.Count == 0) return;

        var score = await GetOrCreateCurrentScoreAsync(now, ct);

        foreach (var ticket in finalTickets)
        {
            if (!ticket.SlaExcluded)
            {
                var resolvedAt = ticket.UpdatedAt;
                if (resolvedAt <= ticket.SlaDueAt)
                    score.RecordWithinSla(now);
                else
                {
                    var hoursOverdue = (int)Math.Ceiling((resolvedAt - ticket.SlaDueAt).TotalHours);
                    score.RecordBreached(hoursOverdue, now);
                }
            }

            ticket.MarkSlaScoreApplied();
        }

        await db.SaveChangesAsync(ct);
        await scoreRepository.SaveChangesAsync(ct);
    }

    // 2 — Mark tickets that just exceeded their SLA deadline
    private async Task DetectNewBreachesAsync(DateTime now, CancellationToken ct)
    {
        var newlyBreached = await db.Set<Ticket>()
            .Where(t =>
                t.SlaBreachedAt == null &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Cancelled &&
                t.SlaDueAt < now)
            .ToListAsync(ct);

        foreach (var ticket in newlyBreached)
            ticket.MarkSlaBreached(now);

        if (newlyBreached.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            foreach (var ticket in newlyBreached)
            {
                foreach (var evt in ticket.DomainEvents)
                    await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, null, evt, ct);
                ticket.ClearDomainEvents();
            }
        }
    }

    // 3 — Apply -5 penalty every 2 hours for unassigned breached tickets
    private async Task ApplyUnassignedPenaltiesAsync(DateTime now, CancellationToken ct)
    {
        var unassignedBreached = await db.Set<Ticket>()
            .Where(t =>
                t.SlaBreachedAt != null &&
                t.AssigneeId == null &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Cancelled)
            .ToListAsync(ct);

        if (unassignedBreached.Count == 0) return;

        var score = await GetOrCreateCurrentScoreAsync(now, ct);

        foreach (var ticket in unassignedBreached)
        {
            var hoursBreached = (now - ticket.SlaDueAt).TotalHours;
            var pendingWindows = (int)(hoursBreached / 2) - ticket.SlaUnassignedPenaltyCount;

            for (var i = 0; i < pendingWindows; i++)
            {
                score.ApplyUnassignedPenalty(now);
                ticket.IncrementUnassignedPenaltyCount();
            }
        }

        await db.SaveChangesAsync(ct);
        await scoreRepository.SaveChangesAsync(ct);
    }

    // 4 — Auto-assign unassigned breached tickets to the Manager with lowest active load
    private async Task AutoAssignBreachedTicketsAsync(DateTime now, CancellationToken ct)
    {
        var candidates = await db.Set<Ticket>()
            .Where(t =>
                t.SlaBreachedAt != null &&
                t.AssigneeId == null &&
                t.AutoAssignedAt == null &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Cancelled)
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        var managerId = await GetManagerWithLowestLoadAsync(ct);
        if (managerId == null) return;

        foreach (var ticket in candidates)
            ticket.AutoAssign(managerId.Value, "Auto-assigned: Manager with lowest active ticket count.", now);

        await db.SaveChangesAsync(ct);
        foreach (var ticket in candidates)
        {
            foreach (var evt in ticket.DomainEvents)
                await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, null, evt, ct);
            ticket.ClearDomainEvents();
        }
    }

    // 5 — Auto-cancel tickets that have been auto-assigned for 10h without resolution
    private async Task AutoCancelTimedOutTicketsAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddHours(-AutoCancelHoursAfterAutoAssign);

        var timedOut = await db.Set<Ticket>()
            .Where(t =>
                t.AutoAssignedAt != null &&
                t.AutoAssignedAt <= cutoff &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Cancelled)
            .ToListAsync(ct);

        foreach (var ticket in timedOut)
            ticket.AutoCancel(AutoCancelReason, now);

        if (timedOut.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            foreach (var ticket in timedOut)
            {
                foreach (var evt in ticket.DomainEvents)
                    await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, null, evt, ct);
                ticket.ClearDomainEvents();
            }
        }
    }

    private async Task<SlaMonthlyScore> GetOrCreateCurrentScoreAsync(DateTime now, CancellationToken ct)
    {
        var score = await scoreRepository.GetForMonthAsync(now.Year, now.Month, ct);
        if (score is not null) return score;

        score = SlaMonthlyScore.Create(now.Year, now.Month, now);
        await scoreRepository.AddAsync(score, ct);
        return score;
    }

    private async Task<Guid?> GetManagerWithLowestLoadAsync(CancellationToken ct)
    {
        var tickets = db.Set<Ticket>().AsNoTracking();

        return await db.Set<User>()
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Manager)
            .Select(u => new
            {
                u.Id,
                u.CreatedAt,
                ActiveCount = tickets.Count(t => t.AssigneeId == u.Id && t.Status == TicketStatus.InProgress)
            })
            .OrderBy(x => x.ActiveCount)
            .ThenBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
    }
}
