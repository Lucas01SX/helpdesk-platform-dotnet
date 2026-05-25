using System.Security.Claims;
using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Application.UseCases;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Infrastructure.Queries;
using Helpdesk.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Helpdesk.API.Controllers;

[Route("api/tickets")]
[Authorize]
public sealed class TicketsController(
    CreateTicketUseCase createTicketUseCase,
    AssignTicketUseCase assignTicketUseCase,
    ResolveTicketUseCase resolveTicketUseCase,
    CancelTicketUseCase cancelTicketUseCase,
    TransferTicketUseCase transferTicketUseCase,
    ChangePriorityUseCase changePriorityUseCase,
    AddCommentUseCase addCommentUseCase,
    UploadAttachmentUseCase uploadAttachmentUseCase,
    GetAttachmentFileUseCase getAttachmentFileUseCase,
    TicketQueryService queryService) : ApiControllerBase
{
    private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string ActorRole => User.FindFirstValue(ClaimTypes.Role)!;

    // POST /api/tickets — Customer only
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var enriched = request with { CustomerId = ActorId };
        var result = await createTicketUseCase.ExecuteAsync(enriched, ct);
        return result.IsSuccess
            ? StatusCode(201, Success(new { ticketId = result.Value }))
            : BadRequest(Failure(result.Error!));
    }

    // POST /api/tickets/{id}/assign — Agent/Manager assigns themselves
    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> AssignTicket(Guid id, CancellationToken ct)
    {
        var result = await assignTicketUseCase.ExecuteAsync(id, ActorId, ct);
        return MapResult(result);
    }

    // POST /api/tickets/{id}/resolve
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> ResolveTicket(Guid id, [FromBody] ResolveTicketRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId };
        var result = await resolveTicketUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // POST /api/tickets/{id}/cancel — Customer (own ticket) or Manager
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelTicket(Guid id, [FromBody] CancelTicketRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId, ActorRole = ActorRole };
        var result = await cancelTicketUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // POST /api/tickets/{id}/transfer
    [HttpPost("{id:guid}/transfer")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> TransferTicket(Guid id, [FromBody] TransferTicketRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId };
        var result = await transferTicketUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // PATCH /api/tickets/{id}/priority
    [HttpPatch("{id:guid}/priority")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> ChangePriority(Guid id, [FromBody] ChangePriorityRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId };
        var result = await changePriorityUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // GET /api/tickets?page=1&limit=20
    [HttpGet]
    public async Task<IActionResult> ListTickets(
        [FromQuery] int page = 1, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit is < 1 or > 100) limit = 20;

        var result = await queryService.ListAsync(ActorId, ActorRole, page, limit, ct);
        return Ok(Success(result));
    }

    // GET /api/tickets/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id, CancellationToken ct)
    {
        var ticket = await queryService.GetByIdAsync(id, ActorId, ActorRole, ct);
        return ticket is not null
            ? Ok(Success(ticket))
            : NotFound(Failure(TicketAppErrors.TicketNotFound));
    }

    // POST /api/tickets/{id}/comments
    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, AuthorId = ActorId, AuthorRole = ActorRole };
        var result = await addCommentUseCase.ExecuteAsync(enriched, ct);
        return result.IsSuccess
            ? StatusCode(201, Success(new { commentId = result.Value }))
            : MapCommentResult(result.Error!);
    }

    // GET /api/tickets/{id}/comments
    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> ListComments(Guid id, CancellationToken ct)
    {
        var visible = await queryService.TicketVisibleToActorAsync(id, ActorId, ActorRole, ct);
        if (!visible)
            return NotFound(Failure(TicketAppErrors.TicketNotFound));

        var comments = await queryService.ListCommentsAsync(id, ActorId, ActorRole, ct);
        return Ok(Success(comments));
    }

    // POST /api/tickets/{id}/attachments
    // Note: [RequestSizeLimit] is intentionally omitted. ASP.NET Core would return an
    // unhandled 413 (bypassing GlobalExceptionHandlerMiddleware) instead of the expected
    // 400 from the use case size check. Size enforcement happens inside UploadAttachmentUseCase.
    [HttpPost("{id:guid}/attachments")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    public async Task<IActionResult> UploadAttachment(
        Guid id, IFormFile? file, [FromQuery] string visibility = "Public", CancellationToken ct = default)
    {
        var request = new UploadAttachmentRequest(
            TicketId: id,
            UploadedBy: ActorId,
            UploaderRole: ActorRole,
            FileName: file?.FileName ?? string.Empty,
            FileContent: file?.OpenReadStream() ?? Stream.Null,
            ContentType: file?.ContentType ?? "application/octet-stream",
            SizeBytes: file?.Length ?? 0,
            Visibility: visibility);

        var result = await uploadAttachmentUseCase.ExecuteAsync(request, ct);
        return result.IsSuccess
            ? StatusCode(201, Success(new { attachmentId = result.Value }))
            : MapAttachmentResult(result.Error!);
    }

    // GET /api/tickets/{id}/attachments
    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> ListAttachments(Guid id, CancellationToken ct)
    {
        var visible = await queryService.TicketVisibleToActorAsync(id, ActorId, ActorRole, ct);
        if (!visible)
            return NotFound(Failure(TicketAppErrors.TicketNotFound));

        var attachments = await queryService.ListAttachmentsAsync(id, ActorId, ActorRole, ct);
        return Ok(Success(attachments));
    }

    // GET /api/tickets/{id}/attachments/{attachmentId}
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await getAttachmentFileUseCase.ExecuteAsync(attachmentId, ActorId, ActorRole, ct);
        if (!result.IsSuccess)
            return MapAttachmentResult(result.Error!);

        var file = result.Value!;
        return File(file.Stream, file.ContentType, file.FileName);
    }

    private IActionResult MapCommentResult(Error error) => error.Code switch
    {
        "ticket.not_found" => NotFound(Failure(error)),
        "comment.forbidden" or "comment.internal_forbidden" => StatusCode(403, Failure(error)),
        _ => BadRequest(Failure(error))
    };

    private IActionResult MapAttachmentResult(Error error) => error.Code switch
    {
        "ticket.not_found" or "attachment.not_found"
            => NotFound(Failure(error)),
        "attachment.forbidden" or "attachment.internal_forbidden" or "attachment.download_forbidden"
            => StatusCode(403, Failure(error)),
        "attachment.ticket_closed"
            => Conflict(Failure(error)),
        _ => BadRequest(Failure(error))
    };

    private IActionResult MapResult(Helpdesk.Shared.Results.Result result)
    {
        if (result.IsSuccess) return NoContent();

        return result.Error!.Code switch
        {
            // 404
            "ticket.not_found"
            or "ticket.assignee_not_found"
                => NotFound(Failure(result.Error)),

            // 403 — actor is not authorized for this operation
            "ticket.forbidden"
            or "ticket.resolution_requires_actor"
            or "ticket.transfer_requires_actor"
            or "ticket.priority_changer_must_be_assignee"
                => StatusCode(403, Failure(result.Error)),

            // 409 — state machine violations and business rule conflicts
            "ticket.cannot_assume_in_progress"
            or "ticket.cannot_assume_final_state"
            or "ticket.cannot_resolve_not_in_progress"
            or "ticket.cannot_resolve_final_state"
            or "ticket.resolution_requires_assignee"
            or "ticket.cannot_cancel_final_state"
            or "ticket.transfer_not_allowed"
            or "ticket.max_priority_changes_reached"
            or "ticket.priority_change_only_in_progress"
                => Conflict(Failure(result.Error)),

            // 400 — input validation failures
            _ => BadRequest(Failure(result.Error))
        };
    }

}
