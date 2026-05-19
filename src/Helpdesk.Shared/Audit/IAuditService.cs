namespace Helpdesk.Shared.Audit;

public interface IAuditService
{
    Task RecordAsync(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        Guid? actorId,
        object payload,
        CancellationToken ct = default);
}
