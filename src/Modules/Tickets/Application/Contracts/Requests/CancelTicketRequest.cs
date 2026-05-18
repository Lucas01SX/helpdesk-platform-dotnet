namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record CancelTicketRequest(
    string? Reason,
    Guid TicketId = default,
    Guid ActorId = default,
    string ActorRole = "");
