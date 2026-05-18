using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record AutoAssigned(
    Guid TicketId,
    Guid ManagerId,
    string Criteria,
    DateTime AssignedAt) : IDomainEvent;
