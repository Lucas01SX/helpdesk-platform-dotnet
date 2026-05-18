using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class AssignTicketUseCase(ITicketRepository repository, IDateTimeProvider clock)
{
    public async Task<Result> ExecuteAsync(Guid ticketId, Guid actorId, CancellationToken ct = default)
    {
        var ticket = await repository.FindByIdAsync(ticketId, ct);
        if (ticket is null) return TicketAppErrors.TicketNotFound;

        var result = ticket.Assume(actorId, clock.UtcNow);
        if (result.IsFailure) return result;

        await repository.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
