using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record TicketCancelled(
    Guid TicketId,
    Guid ActorId,
    string? Reason,
    bool IsAutoCancelled,
    DateTime CancelledAt) : IDomainEvent;
