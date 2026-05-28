using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Persistence;

internal sealed class EfCoreManagerLoadQuery(DbContext context) : IManagerLoadQuery
{
    public Task<Guid?> GetManagerWithLowestActiveTicketCountAsync(CancellationToken ct = default)
    {
        var tickets = context.Set<Ticket>().AsNoTracking();

        return context.Set<User>()
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
