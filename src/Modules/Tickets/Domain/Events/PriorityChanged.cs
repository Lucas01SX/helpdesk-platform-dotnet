using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record PriorityChanged(
    Guid TicketId,
    TicketPriority From,
    TicketPriority To,
    Guid ActorId,
    DateTime ChangedAt) : IDomainEvent;
