using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record TicketResolved(
    Guid TicketId,
    Guid AssigneeId,
    string ResolutionDescription,
    DateTime ResolvedAt) : IDomainEvent;
