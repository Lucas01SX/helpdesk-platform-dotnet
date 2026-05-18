namespace Helpdesk.Modules.Tickets.Application.Contracts.Responses;

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    string Category,
    Guid CustomerId,
    Guid? AssigneeId,
    DateTime CreatedAt,
    DateTime SlaDueAt,
    int PriorityChangeCount,
    int TransferCount);
