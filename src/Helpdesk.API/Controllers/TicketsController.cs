using System.Security.Claims;
using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Application.UseCases;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Infrastructure.Queries;
using Helpdesk.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public sealed class TicketsController(
    CreateTicketUseCase createTicketUseCase,
    AssignTicketUseCase assignTicketUseCase,
    ResolveTicketUseCase resolveTicketUseCase,
    CancelTicketUseCase cancelTicketUseCase,
    TransferTicketUseCase transferTicketUseCase,
    ChangePriorityUseCase changePriorityUseCase,
    TicketQueryService queryService) : ControllerBase
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

    // POST /api/tickets/{id}/assignments — Agent/Manager assigns themselves
    [HttpPost("{id:guid}/assignments")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> AssignTicket(Guid id, CancellationToken ct)
    {
        var result = await assignTicketUseCase.ExecuteAsync(id, ActorId, ct);
        return MapResult(result);
    }

    // POST /api/tickets/{id}/resolution
    [HttpPost("{id:guid}/resolution")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> ResolveTicket(Guid id, [FromBody] ResolveTicketRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId };
        var result = await resolveTicketUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // POST /api/tickets/{id}/cancellation — Customer (own ticket) or Manager
    [HttpPost("{id:guid}/cancellation")]
    public async Task<IActionResult> CancelTicket(Guid id, [FromBody] CancelTicketRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId, ActorRole = ActorRole };
        var result = await cancelTicketUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // POST /api/tickets/{id}/transfers
    [HttpPost("{id:guid}/transfers")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> TransferTicket(Guid id, [FromBody] TransferTicketRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId };
        var result = await transferTicketUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // POST /api/tickets/{id}/priority
    [HttpPost("{id:guid}/priority")]
    [Authorize(Roles = "SupportAgent,Manager")]
    public async Task<IActionResult> ChangePriority(Guid id, [FromBody] ChangePriorityRequest request, CancellationToken ct)
    {
        var enriched = request with { TicketId = id, ActorId = ActorId };
        var result = await changePriorityUseCase.ExecuteAsync(enriched, ct);
        return MapResult(result);
    }

    // GET /api/tickets
    [HttpGet]
    public async Task<IActionResult> ListTickets(CancellationToken ct)
    {
        var tickets = await queryService.ListAsync(ActorId, ActorRole, ct);
        return Ok(Success(tickets));
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

    private IActionResult MapResult(Helpdesk.Shared.Results.Result result)
    {
        if (result.IsSuccess) return NoContent();

        var code = result.Error!.Code;

        if (code == "ticket.not_found")
            return NotFound(Failure(result.Error));

        if (code is "ticket.forbidden"
            or "ticket.resolution_requires_actor"
            or "ticket.transfer_requires_actor"
            or "ticket.priority_changer_must_be_assignee")
            return StatusCode(403, Failure(result.Error));

        if (code.StartsWith("ticket.cannot_")
            || code is "ticket.transfer_not_allowed" or "ticket.max_priority_changes_reached")
            return Conflict(Failure(result.Error));

        return BadRequest(Failure(result.Error));
    }

    private static object Success(object? data) => new { data, timestamp = DateTime.UtcNow };

    private static object Failure(Error error) => new
    {
        success = false,
        error = new { code = error.Code, message = error.Message },
        timestamp = DateTime.UtcNow
    };
}
