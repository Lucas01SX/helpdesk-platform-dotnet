using System.Text.Json;

namespace Helpdesk.API.Audit;

public sealed class AuditEvent
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private AuditEvent() { }

    public static AuditEvent Create(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        Guid? actorId,
        object payload,
        DateTime occurredAt,
        string? correlationId = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        AggregateType = aggregateType,
        AggregateId = aggregateId,
        ActorId = actorId,
        Payload = JsonSerializer.Serialize(payload),
        CorrelationId = correlationId,
        OccurredAt = occurredAt
    };
}
