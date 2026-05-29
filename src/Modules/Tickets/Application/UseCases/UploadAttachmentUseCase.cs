using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;
using Helpdesk.Shared.Security;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class UploadAttachmentUseCase(
    ITicketRepository ticketRepository,
    ITicketAttachmentRepository attachmentRepository,
    IFileStorageService storage,
    IDateTimeProvider clock,
    IAuditService auditService,
    ILogger<UploadAttachmentUseCase> logger)
{
    private const long MaxFileSizeBytes = 10L * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".pdf", ".doc", ".docx", ".txt", ".csv", ".xlsx", ".log"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/csv", "application/csv",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public async Task<Result<Guid>> ExecuteAsync(UploadAttachmentRequest request, CancellationToken ct = default)
    {
        if (request.SizeBytes == 0)
            return TicketAppErrors.AttachmentNoFile;

        if (request.SizeBytes > MaxFileSizeBytes)
            return TicketAppErrors.AttachmentTooLarge;

        var extension = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(extension) || !AllowedMimeTypes.Contains(request.ContentType))
            return TicketAppErrors.AttachmentFileTypeNotAllowed;

        var ticket = await ticketRepository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null)
            return TicketAppErrors.TicketNotFound;

        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Cancelled)
            return TicketAppErrors.AttachmentTicketClosed;

        var isCustomer = request.UploaderRole == RoleNames.Customer;

        if (isCustomer && ticket.CustomerId != request.UploadedBy)
            return TicketAppErrors.TicketNotFound;

        if (!Enum.TryParse<AttachmentVisibility>(request.Visibility, ignoreCase: true, out var visibility))
            visibility = AttachmentVisibility.Public;

        if (isCustomer && visibility == AttachmentVisibility.Internal)
            return TicketAppErrors.AttachmentInternalForbidden;

        // DB-first: determine path without I/O, persist record, then write to disk.
        // If disk write fails, compensate by deleting the DB record.
        // This ensures no orphan files — a missing file for an existing record is detectable.
        var storagePath = storage.BuildPath(request.TicketId, request.FileName);

        var attachment = TicketAttachment.Create(
            request.TicketId, request.UploadedBy, request.FileName, storagePath,
            request.ContentType, request.SizeBytes, visibility, clock.UtcNow);

        await attachmentRepository.AddAsync(attachment, ct);
        await attachmentRepository.SaveChangesAsync(ct);

        try
        {
            await storage.SaveAsync(storagePath, request.FileContent, ct);
        }
        catch (IOException ex)
        {
            logger.LogError(ex,
                "File storage write failed for ticket {TicketId} path {StoragePath}. Compensating.",
                request.TicketId, storagePath);

            try
            {
                await attachmentRepository.DeleteAsync(attachment.Id, CancellationToken.None);
                await attachmentRepository.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception deleteEx)
            {
                logger.LogError(deleteEx,
                    "Compensating delete failed for attachment {AttachmentId}.",
                    attachment.Id);
            }

            throw;
        }

        await auditService.RecordAsync("AttachmentAdded", "Ticket", request.TicketId, request.UploadedBy,
            new { attachment.Id, attachment.FileName, attachment.ContentType }, ct);

        return attachment.Id;
    }
}
