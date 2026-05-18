using Helpdesk.Modules.Tickets.Application.Contracts.Responses;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class GetAttachmentFileUseCase(
    ITicketAttachmentRepository attachmentRepository,
    IFileStorageService storage)
{
    public async Task<Result<AttachmentFileResponse>> ExecuteAsync(
        Guid attachmentId, Guid actorId, string actorRole, CancellationToken ct = default)
    {
        var attachment = await attachmentRepository.FindByIdAsync(attachmentId, ct);
        if (attachment is null)
            return TicketAppErrors.AttachmentNotFound;

        if (actorRole == "Customer" && attachment.Visibility == AttachmentVisibility.Internal)
            return TicketAppErrors.AttachmentDownloadForbidden;

        var stream = await storage.GetAsync(attachment.StoragePath, ct);
        if (stream is null)
            return TicketAppErrors.AttachmentNotFound;

        return new AttachmentFileResponse(stream, attachment.ContentType, attachment.FileName);
    }
}
