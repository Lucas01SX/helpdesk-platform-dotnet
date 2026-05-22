using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Tickets.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(DbContext context) : ITicketRepository
{
    private readonly DbSet<Ticket> _tickets = context.Set<Ticket>();

    public async Task<Ticket?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
        => await _tickets.AddAsync(ticket, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task<bool> IsAssignableUserAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await context.Database
            .SqlQuery<int>(
                $"""
                SELECT COUNT(*)::int
                FROM users
                WHERE id = {userId}
                  AND "Role" IN ('SupportAgent', 'Manager')
                """)
            .SingleAsync(ct);
        return count > 0;
    }
}
