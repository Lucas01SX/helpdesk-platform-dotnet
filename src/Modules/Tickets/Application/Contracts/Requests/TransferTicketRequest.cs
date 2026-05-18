namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record TransferTicketRequest(
    Guid NewAssigneeId,
    string? Reason,
    Guid TicketId = default,
    Guid ActorId = default);
