using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Errors;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class CreateTicketUseCase(ITicketRepository repository, IDateTimeProvider clock)
{
    public async Task<Result<Guid>> ExecuteAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<Guid>.Fail(new Error("ticket.title_required", "Title is required."));

        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<Guid>.Fail(new Error("ticket.description_required", "Description is required."));

        var ticket = Ticket.Create(
            request.Title,
            request.Description,
            request.CustomerId,
            request.Priority,
            request.Category,
            clock.UtcNow);

        await repository.AddAsync(ticket, ct);
        await repository.SaveChangesAsync(ct);

        return ticket.Id;
    }
}
