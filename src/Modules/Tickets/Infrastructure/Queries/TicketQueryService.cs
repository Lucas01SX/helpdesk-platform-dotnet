using Helpdesk.Modules.Tickets.Application.Contracts.Responses;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Modules.Tickets.Infrastructure.Queries;

public sealed class TicketQueryService(DbContext context)
{
    private readonly DbSet<Ticket> _tickets = context.Set<Ticket>();
    private readonly DbSet<TicketComment> _comments = context.Set<TicketComment>();
    private readonly DbSet<TicketAttachment> _attachments = context.Set<TicketAttachment>();

    public async Task<PagedResult<TicketSummaryResponse>> ListAsync(
        Guid actorId, string actorRole, int page, int limit, CancellationToken ct = default)
    {
        var query = _tickets.AsNoTracking();

        if (actorRole == RoleNames.Customer)
            query = query.Where(t => t.CustomerId == actorId);

        var ordered = query.OrderByDescending(t => t.CreatedAt);

        var total = await ordered.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(t => new TicketSummaryResponse(
                t.Id, t.Title, t.Status.ToString(), t.Priority.ToString(),
                t.Category.ToString(), t.CustomerId, t.AssigneeId, t.CreatedAt, t.SlaDueAt))
            .ToListAsync(ct);

        return new PagedResult<TicketSummaryResponse>(items, total, page, limit);
    }

    public async Task<TicketResponse?> GetByIdAsync(
        Guid id, Guid actorId, string actorRole, CancellationToken ct = default)
    {
        var query = _tickets.AsNoTracking().Where(t => t.Id == id);

        if (actorRole == RoleNames.Customer)
            query = query.Where(t => t.CustomerId == actorId);

        return await query
            .Select(t => new TicketResponse(
                t.Id, t.Title, t.Description, t.Status.ToString(), t.Priority.ToString(),
                t.Category.ToString(), t.CustomerId, t.AssigneeId, t.CreatedAt, t.SlaDueAt,
                t.PriorityChangeCount, t.TransferCount))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> TicketVisibleToActorAsync(
        Guid ticketId, Guid actorId, string actorRole, CancellationToken ct = default)
    {
        var query = _tickets.AsNoTracking().Where(t => t.Id == ticketId);

        if (actorRole == RoleNames.Customer)
            query = query.Where(t => t.CustomerId == actorId);

        return await query.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<CommentResponse>> ListCommentsAsync(
        Guid ticketId, Guid actorId, string actorRole, CancellationToken ct = default)
    {
        var visibilityFilter = actorRole == RoleNames.Customer
            ? CommentVisibility.Public
            : (CommentVisibility?)null;

        var query = _comments.AsNoTracking().Where(c => c.TicketId == ticketId);

        if (visibilityFilter.HasValue)
            query = query.Where(c => c.Visibility == visibilityFilter.Value);

        return await query
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentResponse(
                c.Id, c.TicketId, c.AuthorId, c.Content, c.Visibility.ToString(), c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttachmentResponse>> ListAttachmentsAsync(
        Guid ticketId, Guid actorId, string actorRole, CancellationToken ct = default)
    {
        var visibilityFilter = actorRole == RoleNames.Customer
            ? AttachmentVisibility.Public
            : (AttachmentVisibility?)null;

        var query = _attachments.AsNoTracking().Where(a => a.TicketId == ticketId);

        if (visibilityFilter.HasValue)
            query = query.Where(a => a.Visibility == visibilityFilter.Value);

        return await query
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentResponse(
                a.Id, a.TicketId, a.UploadedBy, a.FileName, a.ContentType,
                a.SizeBytes, a.Visibility.ToString(), a.CreatedAt))
            .ToListAsync(ct);
    }
}
