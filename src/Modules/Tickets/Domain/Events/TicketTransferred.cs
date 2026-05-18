using Helpdesk.Shared.Domain;

namespace Helpdesk.Modules.Tickets.Domain.Events;

public sealed record TicketTransferred(
    Guid TicketId,
    Guid FromAssigneeId,
    Guid ToAssigneeId,
    string? Reason,
    DateTime TransferredAt) : IDomainEvent;
