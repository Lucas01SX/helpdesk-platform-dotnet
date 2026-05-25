namespace Helpdesk.Shared.Abstractions;

public interface IAssignableUserChecker
{
    Task<bool> IsAssignableUserAsync(Guid userId, CancellationToken ct = default);
}
