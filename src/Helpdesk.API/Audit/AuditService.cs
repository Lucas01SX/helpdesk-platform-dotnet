using Helpdesk.API.Persistence;
using Helpdesk.Shared.Abstractions;
using Helpdesk.Shared.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.API.Audit;

internal sealed class AuditService(IServiceScopeFactory scopeFactory, IDateTimeProvider clock) : IAuditService
{
    public async Task RecordAsync(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        Guid? actorId,
        object payload,
        CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var auditEvent = AuditEvent.Create(eventType, aggregateType, aggregateId, actorId, payload, clock.UtcNow);
        db.Set<AuditEvent>().Add(auditEvent);
        await db.SaveChangesAsync(ct);
    }
}
