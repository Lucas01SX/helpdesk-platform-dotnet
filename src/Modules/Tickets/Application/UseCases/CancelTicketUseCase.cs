using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Errors;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class CancelTicketUseCase(ITicketRepository repository, IDateTimeProvider clock)
{
    public async Task<Result> ExecuteAsync(CancelTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await repository.FindByIdAsync(request.TicketId, ct);
        if (ticket is null) return TicketAppErrors.TicketNotFound;

        var now = clock.UtcNow;

        if (request.ActorRole == "Manager")
            return await ApplyAndSaveAsync(ticket.CancelByManager(request.ActorId, request.Reason, now), ct);

        // Customer: must be the ticket creator
        if (ticket.CustomerId != request.ActorId)
            return TicketAppErrors.Forbidden;

        return await ApplyAndSaveAsync(ticket.CancelByCustomer(request.ActorId, request.Reason, now), ct);
    }

    private async Task<Result> ApplyAndSaveAsync(Result domainResult, CancellationToken ct)
    {
        if (domainResult.IsFailure) return domainResult;
        await repository.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
