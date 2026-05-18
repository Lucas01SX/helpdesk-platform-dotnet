using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;

namespace Helpdesk.Modules.Tickets.Domain.Interfaces;

public interface ITicketCommentRepository
{
    Task AddAsync(TicketComment comment, CancellationToken ct = default);
    Task<IReadOnlyList<TicketComment>> ListByTicketAsync(
        Guid ticketId, CommentVisibility? visibilityFilter, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
