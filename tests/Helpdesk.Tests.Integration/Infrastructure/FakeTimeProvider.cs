using Helpdesk.Shared.Abstractions;

namespace Helpdesk.Tests.Integration.Infrastructure;

public sealed class FakeTimeProvider : IDateTimeProvider
{
    private DateTime _now = DateTime.UtcNow;

    public DateTime UtcNow => _now;

    public void SetUtcNow(DateTime now) => _now = DateTime.SpecifyKind(now, DateTimeKind.Utc);

    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}
