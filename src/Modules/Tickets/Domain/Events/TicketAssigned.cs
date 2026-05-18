using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record TicketAssigned(
    Guid TicketId,
    Guid AssigneeId,
    DateTime AssignedAt) : IDomainEvent;
