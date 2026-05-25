using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class TransferTicketUseCase(
    ITicketRepository repository,
    IAssignableUserChecker userChecker,
    IDateTimeProvider clock,
    IAuditService auditService)
{
    public async Task<Result> ExecuteAsync(TransferTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await repository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null) return TicketAppErrors.TicketNotFound;

        var isAssignable = await userChecker.IsAssignableUserAsync(request.NewAssigneeId, ct);
        if (!isAssignable) return TicketAppErrors.AssigneeNotFound;

        var result = ticket.Transfer(request.ActorId, request.NewAssigneeId, request.Reason, clock.UtcNow);
        if (result.IsFailure) return result;

        await repository.SaveChangesAsync(ct);

        foreach (var evt in ticket.DomainEvents)
            await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, request.ActorId, evt, ct);
        ticket.ClearDomainEvents();

        return Result.Ok();
    }
}
