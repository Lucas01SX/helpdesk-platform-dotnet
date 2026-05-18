using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Tickets.Infrastructure.Persistence.Repositories;

internal sealed class TicketAttachmentRepository(DbContext context) : ITicketAttachmentRepository
{
    private readonly DbSet<TicketAttachment> _attachments = context.Set<TicketAttachment>();

    public async Task AddAsync(TicketAttachment attachment, CancellationToken ct = default)
        => await _attachments.AddAsync(attachment, ct);

    public async Task<TicketAttachment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<TicketAttachment>> ListByTicketAsync(
        Guid ticketId, AttachmentVisibility? visibilityFilter, CancellationToken ct = default)
    {
        var query = _attachments.AsNoTracking().Where(a => a.TicketId == ticketId);

        if (visibilityFilter.HasValue)
            query = query.Where(a => a.Visibility == visibilityFilter.Value);

        return await query.OrderBy(a => a.CreatedAt).ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
