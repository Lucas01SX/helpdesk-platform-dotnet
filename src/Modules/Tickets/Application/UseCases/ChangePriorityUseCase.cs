using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class ChangePriorityUseCase(ITicketRepository repository, IDateTimeProvider clock)
{
    public async Task<Result> ExecuteAsync(ChangePriorityRequest request, CancellationToken ct = default)
    {
        var ticket = await repository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null) return TicketAppErrors.TicketNotFound;

        var result = ticket.ChangePriority(request.ActorId, request.Priority, clock.UtcNow);
        if (result.IsFailure) return result;

        await repository.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
