using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record TicketCreated(
    Guid TicketId,
    Guid CustomerId,
    TicketPriority Priority,
    TicketCategory Category,
    DateTime CreatedAt) : IDomainEvent;
