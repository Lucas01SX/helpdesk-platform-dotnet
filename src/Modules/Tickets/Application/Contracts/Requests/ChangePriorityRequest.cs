using Helpdesk.Modules.Tickets.Domain.Enums;

namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record ChangePriorityRequest(
    TicketPriority Priority,
    Guid TicketId = default,
    Guid ActorId = default);
