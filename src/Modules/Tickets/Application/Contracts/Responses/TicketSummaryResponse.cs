namespace Helpdesk.Modules.Tickets.Application.Contracts.Responses;

public sealed record TicketSummaryResponse(
    Guid Id,
    string Title,
    string Status,
    string Priority,
    string Category,
    Guid CustomerId,
    Guid? AssigneeId,
    DateTime CreatedAt,
    DateTime SlaDueAt);
