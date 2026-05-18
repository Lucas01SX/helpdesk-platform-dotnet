using Helpdesk.Modules.Tickets.Domain.Enums;

namespace Helpdesk.Modules.Tickets.Domain.Entities;

public sealed class TicketAttachment
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid UploadedBy { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public AttachmentVisibility Visibility { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TicketAttachment() { }

    public static TicketAttachment Create(
        Guid ticketId, Guid uploadedBy, string fileName, string storagePath,
        string contentType, long sizeBytes, AttachmentVisibility visibility, DateTime now)
    {
        return new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UploadedBy = uploadedBy,
            FileName = fileName,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Visibility = visibility,
            CreatedAt = now
        };
    }
}
