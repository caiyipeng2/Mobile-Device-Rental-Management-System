namespace DeviceRental.Infrastructure.Persistence.Records;

public sealed class OutboxMessageRecord
{
    public Guid EventId { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string AggregateType { get; set; } = string.Empty;

    public string AggregateId { get; set; } = string.Empty;

    public long AggregateVersion { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public int? PayloadSchemaVersion { get; set; }

    public string? PayloadKeyVersion { get; set; }

    public byte[]? PayloadCiphertext { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public string Status { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public Guid? LeaseId { get; set; }

    public string? LockedBy { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? SendingStartedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? CanceledAt { get; set; }

    public DateTimeOffset? FailedAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? PayloadPurgedAt { get; set; }
}
