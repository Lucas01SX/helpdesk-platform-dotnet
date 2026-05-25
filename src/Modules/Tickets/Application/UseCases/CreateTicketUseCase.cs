using Helpdesk.Modules.Tickets.Application.Contracts.Requests;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Helpdesk.Shared.Errors;
using Helpdesk.Shared.Notifications;
using Helpdesk.Shared.Results;

namespace Helpdesk.Modules.Tickets.Application.UseCases;

public sealed class CreateTicketUseCase(
    ITicketRepository repository,
    IDateTimeProvider clock,
    IAuditService auditService,
    INotificationService notifications)
{
    public async Task<Result<Guid>> ExecuteAsync(CreateTicketRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<Guid>.Fail(new Error("ticket.title_required", "Title is required."));

        if (request.Title.Length > 200)
            return Result<Guid>.Fail(new Error("ticket.title_too_long", "Title must not exceed 200 characters."));

        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<Guid>.Fail(new Error("ticket.description_required", "Description is required."));

        if (request.Description.Length > 2000)
            return Result<Guid>.Fail(new Error("ticket.description_too_long", "Description must not exceed 2000 characters."));

        var ticket = Ticket.Create(
            request.Title,
            request.Description,
            request.CustomerId,
            request.Priority,
            request.Category,
            clock.UtcNow);

        await repository.AddAsync(ticket, ct);
        await repository.SaveChangesAsync(ct);

        foreach (var evt in ticket.DomainEvents)
            await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, null, evt, ct);
        ticket.ClearDomainEvents();

        await notifications.NotifyTicketCreatedAsync(request.CustomerId, ticket.Id, ticket.Title, ct);

        return ticket.Id;
    }
}
