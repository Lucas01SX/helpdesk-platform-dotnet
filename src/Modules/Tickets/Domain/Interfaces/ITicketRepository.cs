using Helpdesk.Modules.Tickets.Domain.Entities;

namespace Helpdesk.Modules.Tickets.Domain.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<bool> IsAssignableUserAsync(Guid userId, CancellationToken ct = default);
}
