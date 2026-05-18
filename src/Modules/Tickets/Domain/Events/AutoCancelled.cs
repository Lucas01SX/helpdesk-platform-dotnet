using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record AutoCancelled(
    Guid TicketId,
    string Reason,
    DateTime CancelledAt) : IDomainEvent;
