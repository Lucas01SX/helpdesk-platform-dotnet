using System.Text.Json.Serialization;

namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record AddCommentRequest(
    string Content,
    string Visibility = "Public",
    [property: JsonIgnore] Guid TicketId = default,
    [property: JsonIgnore] Guid AuthorId = default,
    [property: JsonIgnore] string AuthorRole = "");
