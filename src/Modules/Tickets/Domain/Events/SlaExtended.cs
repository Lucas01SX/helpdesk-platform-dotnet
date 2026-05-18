using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record SlaExtended(
    Guid TicketId,
    DateTime OldDeadline,
    DateTime NewDeadline,
    DateTime ExtendedAt) : IDomainEvent;
