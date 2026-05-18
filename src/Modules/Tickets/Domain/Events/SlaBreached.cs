using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record SlaBreached(
    Guid TicketId,
    DateTime DeadlineAt,
    DateTime BreachedAt) : IDomainEvent;
