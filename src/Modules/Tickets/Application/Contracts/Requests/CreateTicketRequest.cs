using Helpdesk.Modules.Tickets.Domain.Enums;

namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority = TicketPriority.Low,
    TicketCategory Category = TicketCategory.Support,
    Guid CustomerId = default);
