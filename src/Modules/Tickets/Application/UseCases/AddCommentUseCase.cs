using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class AddCommentUseCase(
    ITicketRepository ticketRepository,
    ITicketCommentRepository commentRepository,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    public async Task<Result<Guid>> ExecuteAsync(AddCommentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return TicketAppErrors.CommentContentRequired;

        if (request.Content.Length > 4000)
            return TicketAppErrors.CommentContentTooLong;

        var ticket = await ticketRepository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null)
            return TicketAppErrors.TicketNotFound;

        var isCustomer = request.AuthorRole == "Customer";

        if (isCustomer && ticket.CustomerId != request.AuthorId)
            return TicketAppErrors.TicketNotFound;

        if (!Enum.TryParse<CommentVisibility>(request.Visibility, ignoreCase: true, out var visibility))
            visibility = CommentVisibility.Public;

        if (isCustomer && visibility == CommentVisibility.Internal)
            return TicketAppErrors.CommentInternalForbidden;

        var comment = TicketComment.Create(
            request.TicketId, request.AuthorId, request.Content, visibility, clock.UtcNow);

        await commentRepository.AddAsync(comment, ct);
        await commentRepository.SaveChangesAsync(ct);

        await auditService.RecordAsync("CommentAdded", "Ticket", request.TicketId, request.AuthorId,
            new { comment.Id, request.Visibility }, ct);

        return comment.Id;
    }
}
