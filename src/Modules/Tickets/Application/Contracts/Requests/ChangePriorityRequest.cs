using System.Text.Json.Serialization;
using Helpdesk.Modules.Tickets.Domain.Enums;

namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record ChangePriorityRequest(
    TicketPriority Priority,
    [property: JsonIgnore] Guid TicketId = default,
    [property: JsonIgnore] Guid ActorId = default);
