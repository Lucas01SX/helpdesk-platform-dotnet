using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class UploadAttachmentUseCase(
    ITicketRepository ticketRepository,
    ITicketAttachmentRepository attachmentRepository,
    IFileStorageService storage,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    private const long MaxFileSizeBytes = 10L * 1024 * 1024;

    public async Task<Result<Guid>> ExecuteAsync(UploadAttachmentRequest request, CancellationToken ct = default)
    {
        if (request.SizeBytes == 0)
            return TicketAppErrors.AttachmentNoFile;

        if (request.SizeBytes > MaxFileSizeBytes)
            return TicketAppErrors.AttachmentTooLarge;

        var ticket = await ticketRepository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null)
            return TicketAppErrors.TicketNotFound;

        var isCustomer = request.UploaderRole == "Customer";

        if (isCustomer && ticket.CustomerId != request.UploadedBy)
            return TicketAppErrors.TicketNotFound;

        if (!Enum.TryParse<AttachmentVisibility>(request.Visibility, ignoreCase: true, out var visibility))
            visibility = AttachmentVisibility.Public;

        if (isCustomer && visibility == AttachmentVisibility.Internal)
            return TicketAppErrors.AttachmentInternalForbidden;

        var storagePath = await storage.SaveAsync(
            request.TicketId, request.FileName, request.FileContent, ct);

        var attachment = TicketAttachment.Create(
            request.TicketId, request.UploadedBy, request.FileName, storagePath,
            request.ContentType, request.SizeBytes, visibility, clock.UtcNow);

        await attachmentRepository.AddAsync(attachment, ct);
        await attachmentRepository.SaveChangesAsync(ct);

        await auditService.RecordAsync("AttachmentAdded", "Ticket", request.TicketId, request.UploadedBy,
            new { attachment.Id, attachment.FileName, attachment.ContentType }, ct);

        return attachment.Id;
    }
}
