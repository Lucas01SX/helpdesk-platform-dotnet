namespace Helpdesk.Modules.Tickets.Application.Contracts.Responses;

public sealed record AttachmentFileResponse(Stream Stream, string ContentType, string FileName);
