using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record StatusChanged(
    Guid TicketId,
    TicketStatus From,
    TicketStatus To,
    DateTime ChangedAt) : IDomainEvent;
