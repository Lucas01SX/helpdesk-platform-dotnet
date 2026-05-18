using Helpdesk.Modules.Tickets.Application.Contracts.Responses;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Tickets.Infrastructure.Queries;

public sealed class TicketQueryService(DbContext context)
{
    private readonly DbSet<Ticket> _tickets = context.Set<Ticket>();

    public async Task<IReadOnlyList<TicketSummaryResponse>> ListAsync(
        Guid actorId, string actorRole, CancellationToken ct = default)
    {
        var query = _tickets.AsNoTracking();

        if (actorRole == "Customer")
            query = query.Where(t => t.CustomerId == actorId);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TicketSummaryResponse(
                t.Id, t.Title, t.Status.ToString(), t.Priority.ToString(),
                t.Category.ToString(), t.CustomerId, t.AssigneeId, t.CreatedAt, t.SlaDueAt))
            .ToListAsync(ct);
    }

    public async Task<TicketResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _tickets.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TicketResponse(
                t.Id, t.Title, t.Description, t.Status.ToString(), t.Priority.ToString(),
                t.Category.ToString(), t.CustomerId, t.AssigneeId, t.CreatedAt, t.SlaDueAt,
                t.PriorityChangeCount, t.TransferCount))
            .FirstOrDefaultAsync(ct);
    }
}
