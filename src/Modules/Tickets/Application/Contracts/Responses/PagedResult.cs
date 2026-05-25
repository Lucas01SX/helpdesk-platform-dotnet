namespace Helpdesk.Modules.Tickets.Application.Contracts.Responses;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int Limit);
