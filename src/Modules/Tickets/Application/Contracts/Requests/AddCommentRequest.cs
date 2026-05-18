namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record AddCommentRequest(
    string Content,
    string Visibility = "Public",
    Guid TicketId = default,
    Guid AuthorId = default,
    string AuthorRole = "");
