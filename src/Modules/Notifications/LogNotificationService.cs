using Helpdesk.Shared.Notifications;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Modules.Notifications;

internal sealed class LogNotificationService(ILogger<LogNotificationService> logger) : INotificationService
{
    public Task NotifyTicketCreatedAsync(Guid customerId, Guid ticketId, string title, CancellationToken ct = default)
    {
        logger.LogInformation(
            "notification: ticket created — customerId={CustomerId} ticketId={TicketId} title={Title}",
            customerId, ticketId, title);
        return Task.CompletedTask;
    }

    public Task NotifyTicketAssignedAsync(Guid customerId, Guid ticketId, Guid agentId, CancellationToken ct = default)
    {
        logger.LogInformation(
            "notification: ticket assigned — customerId={CustomerId} ticketId={TicketId} agentId={AgentId}",
            customerId, ticketId, agentId);
        return Task.CompletedTask;
    }

    public Task NotifyTicketResolvedAsync(Guid customerId, Guid ticketId, CancellationToken ct = default)
    {
        logger.LogInformation(
            "notification: ticket resolved — customerId={CustomerId} ticketId={TicketId}",
            customerId, ticketId);
        return Task.CompletedTask;
    }
}
