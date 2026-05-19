using System.Text.Json.Serialization;

namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record TransferTicketRequest(
    Guid NewAssigneeId,
    string? Reason,
    [property: JsonIgnore] Guid TicketId = default,
    [property: JsonIgnore] Guid ActorId = default);
