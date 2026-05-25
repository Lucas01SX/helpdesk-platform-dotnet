namespace Helpdesk.Shared.Notifications;

public interface INotificationService
{
    Task NotifyTicketCreatedAsync(Guid customerId, Guid ticketId, string title, CancellationToken ct = default);
    Task NotifyTicketAssignedAsync(Guid customerId, Guid ticketId, Guid agentId, CancellationToken ct = default);
    Task NotifyTicketResolvedAsync(Guid customerId, Guid ticketId, CancellationToken ct = default);
}
