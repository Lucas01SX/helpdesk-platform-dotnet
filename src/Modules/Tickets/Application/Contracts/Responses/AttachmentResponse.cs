namespace Helpdesk.Modules.Tickets.Application.Contracts.Responses;

public sealed record AttachmentResponse(
    Guid Id,
    Guid TicketId,
    Guid UploadedBy,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Visibility,
    DateTime CreatedAt);
