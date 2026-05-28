namespace Helpdesk.Shared.Abstractions;

public interface IManagerLoadQuery
{
    Task<Guid?> GetManagerWithLowestActiveTicketCountAsync(CancellationToken ct = default);
}
