using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Tickets.Infrastructure.Persistence.Repositories;

internal sealed class TicketCommentRepository(DbContext context) : ITicketCommentRepository
{
    private readonly DbSet<TicketComment> _comments = context.Set<TicketComment>();

    public async Task AddAsync(TicketComment comment, CancellationToken ct = default)
        => await _comments.AddAsync(comment, ct);

    public async Task<IReadOnlyList<TicketComment>> ListByTicketAsync(
        Guid ticketId, CommentVisibility? visibilityFilter, CancellationToken ct = default)
    {
        var query = _comments.AsNoTracking().Where(c => c.TicketId == ticketId);

        if (visibilityFilter.HasValue)
            query = query.Where(c => c.Visibility == visibilityFilter.Value);

        return await query.OrderBy(c => c.CreatedAt).ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
