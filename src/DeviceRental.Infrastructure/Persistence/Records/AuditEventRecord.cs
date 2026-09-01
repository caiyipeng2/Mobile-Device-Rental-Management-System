namespace DeviceRental.Infrastructure.Persistence.Records;

public sealed class AuditEventRecord
{
    public Guid EventId { get; set; }

    public string ActorKind { get; set; } = string.Empty;

    public Guid? ActorUserId { get; set; }

    public string? ExternalActorIdentifier { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string SubjectType { get; set; } = string.Empty;

    public string SubjectId { get; set; } = string.Empty;

    public string ChangedFieldsJson { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
