using Helpdesk.Shared.Errors;

namespace Helpdesk.Modules.Tickets.Domain.Errors;

public static class TicketAppErrors
{
    public static readonly Error TicketNotFound =
        new("ticket.not_found", "Ticket not found.");

    public static readonly Error Forbidden =
        new("ticket.forbidden", "You do not have permission to perform this action on this ticket.");

    // Comments
    public static readonly Error CommentContentRequired =
        new("comment.content_required", "Comment content is required.");

    public static readonly Error CommentContentTooLong =
        new("comment.content_too_long", "Comment content must not exceed 4000 characters.");

    public static readonly Error CommentInternalForbidden =
        new("comment.internal_forbidden", "Only agents and managers can post internal comments.");

    public static readonly Error CommentTicketForbidden =
        new("comment.forbidden", "You do not have permission to comment on this ticket.");

    // Attachments
    public static readonly Error AttachmentNoFile =
        new("attachment.no_file", "No file was provided.");

    public static readonly Error AttachmentTooLarge =
        new("attachment.file_too_large", "File exceeds the 10 MB limit.");

    public static readonly Error AttachmentInternalForbidden =
        new("attachment.internal_forbidden", "Only agents and managers can upload internal attachments.");

    public static readonly Error AttachmentTicketForbidden =
        new("attachment.forbidden", "You do not have permission to upload to this ticket.");

    public static readonly Error AttachmentNotFound =
        new("attachment.not_found", "Attachment not found.");

    public static readonly Error AttachmentDownloadForbidden =
        new("attachment.download_forbidden", "You do not have permission to download this attachment.");

    public static readonly Error AttachmentFileTypeNotAllowed =
        new("attachment.file_type_not_allowed",
            "File type is not allowed. Accepted formats: jpg, jpeg, png, gif, webp, pdf, doc, docx, txt, csv, xlsx, log.");
}
