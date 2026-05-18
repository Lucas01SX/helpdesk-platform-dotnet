namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record ResolveTicketRequest(
    string Description,
    Guid TicketId = default,
    Guid ActorId = default);
