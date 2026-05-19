using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;
using Helpdesk.Shared.Security;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class CancelTicketUseCase(
    ITicketRepository repository,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    public async Task<Result> ExecuteAsync(CancelTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await repository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null) return TicketAppErrors.TicketNotFound;

        var now = clock.UtcNow;
        Result domainResult;

        if (request.ActorRole == RoleNames.Manager)
            domainResult = ticket.CancelByManager(request.ActorId, request.Reason, now);
        else
        {
            if (ticket.CustomerId != request.ActorId)
                return TicketAppErrors.Forbidden;
            domainResult = ticket.CancelByCustomer(request.ActorId, request.Reason, now);
        }

        if (domainResult.IsFailure) return domainResult;

        await repository.SaveChangesAsync(ct);

        foreach (var evt in ticket.DomainEvents)
            await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, request.ActorId, evt, ct);
        ticket.ClearDomainEvents();

        return Result.Ok();
    }
}
