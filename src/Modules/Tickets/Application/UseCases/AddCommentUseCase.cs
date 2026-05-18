using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class AddCommentUseCase(
    ITicketRepository ticketRepository,
    ITicketCommentRepository commentRepository,
    IDateTimeProvider clock)
{
    public async Task<Result<Guid>> ExecuteAsync(AddCommentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return TicketAppErrors.CommentContentRequired;

        var ticket = await ticketRepository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null)
            return TicketAppErrors.TicketNotFound;

        var isCustomer = request.AuthorRole == "Customer";

        if (isCustomer && ticket.CustomerId != request.AuthorId)
            return TicketAppErrors.CommentTicketForbidden;

        if (!Enum.TryParse<CommentVisibility>(request.Visibility, ignoreCase: true, out var visibility))
            visibility = CommentVisibility.Public;

        if (isCustomer && visibility == CommentVisibility.Internal)
            return TicketAppErrors.CommentInternalForbidden;

        var comment = TicketComment.Create(
            request.TicketId, request.AuthorId, request.Content, visibility, clock.UtcNow);

        await commentRepository.AddAsync(comment, ct);
        await commentRepository.SaveChangesAsync(ct);

        return comment.Id;
    }
}
