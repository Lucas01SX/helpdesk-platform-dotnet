namespace Helpdesk.Shared.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
