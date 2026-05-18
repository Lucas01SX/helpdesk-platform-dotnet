using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;

namespace Helpdesk.Modules.Tickets.Domain.Interfaces;

public interface ITicketAttachmentRepository
{
    Task AddAsync(TicketAttachment attachment, CancellationToken ct = default);
    Task<TicketAttachment?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TicketAttachment>> ListByTicketAsync(
        Guid ticketId, AttachmentVisibility? visibilityFilter, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
