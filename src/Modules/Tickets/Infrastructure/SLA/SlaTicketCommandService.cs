using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Tickets.Infrastructure.SLA;

internal sealed class SlaTicketCommandService(DbContext context, IAuditService auditService) : ISlaTicketCommandService
{
    private readonly DbSet<Ticket> _tickets = context.Set<Ticket>();

    public async Task MarkSlaBreachedAsync(Guid ticketId, DateTime now, CancellationToken ct = default)
    {
        var ticket = await _tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return;
        ticket.MarkSlaBreached(now);
        await context.SaveChangesAsync(ct);
        await DispatchEventsAsync(ticket, null, ct);
    }

    public async Task MarkSlaScoreAppliedAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return;
        ticket.MarkSlaScoreApplied();
        await context.SaveChangesAsync(ct);
    }

    public async Task IncrementUnassignedPenaltyAsync(Guid ticketId, int count, CancellationToken ct = default)
    {
        var ticket = await _tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return;
        for (var i = 0; i < count; i++)
            ticket.IncrementUnassignedPenaltyCount();
        await context.SaveChangesAsync(ct);
    }

    public async Task AutoAssignAsync(Guid ticketId, Guid managerId, string criteria, DateTime now, CancellationToken ct = default)
    {
        var ticket = await _tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return;
        var result = ticket.AutoAssign(managerId, criteria, now);
        if (result.IsFailure) return;
        await context.SaveChangesAsync(ct);
        await DispatchEventsAsync(ticket, null, ct);
    }

    public async Task AutoCancelAsync(Guid ticketId, string reason, DateTime now, CancellationToken ct = default)
    {
        var ticket = await _tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return;
        var result = ticket.AutoCancel(reason, now);
        if (result.IsFailure) return;
        await context.SaveChangesAsync(ct);
        await DispatchEventsAsync(ticket, null, ct);
    }

    private async Task DispatchEventsAsync(Ticket ticket, Guid? actorId, CancellationToken ct)
    {
        foreach (var evt in ticket.DomainEvents)
            await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, actorId, evt, ct);
        ticket.ClearDomainEvents();
    }
}
