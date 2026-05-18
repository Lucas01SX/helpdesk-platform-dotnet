namespace Helpdesk.Modules.Tickets.Application.Contracts.Responses;

public sealed record CommentResponse(
    Guid Id,
    Guid TicketId,
    Guid AuthorId,
    string Content,
    string Visibility,
    DateTime CreatedAt);
