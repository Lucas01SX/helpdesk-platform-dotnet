namespace Helpdesk.Modules.Tickets.Application.Contracts.Requests;

public sealed record UploadAttachmentRequest(
    Guid TicketId,
    Guid UploadedBy,
    string UploaderRole,
    string FileName,
    Stream FileContent,
    string ContentType,
    long SizeBytes,
    string Visibility);
