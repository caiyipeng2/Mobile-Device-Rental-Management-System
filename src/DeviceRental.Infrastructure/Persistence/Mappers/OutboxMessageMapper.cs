using DeviceRental.Domain.Notifications;
using DeviceRental.Infrastructure.Persistence.Records;

namespace DeviceRental.Infrastructure.Persistence.Mappers;

public static class OutboxMessageMapper
{
    public static OutboxMessageRecord ToRecord(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new OutboxMessageRecord
        {
            EventId = message.Id,
            DedupeKey = message.DeduplicationKey,
            EventType = message.MessageType,
            AggregateType = message.AggregateType,
            AggregateId = message.AggregateId,
            AggregateVersion = message.AggregateVersion,
            CorrelationId = message.CorrelationId,
            PayloadSchemaVersion = message.Payload?.SchemaVersion,
            PayloadKeyVersion = message.Payload?.KeyVersion,
            PayloadCiphertext = message.Payload?.Ciphertext,
            CreatedAt = message.CreatedAtUtc,
            AvailableAt = message.AvailableAtUtc,
            Status = FormatStatus(message.Status),
            Attempts = message.AttemptCount,
            LeaseId = message.LeaseId,
            LockedBy = message.LockedBy,
            LockedUntil = message.LockedUntilUtc,
            SendingStartedAt = message.SendingStartedAtUtc,
            ProcessedAt = message.ProcessedAtUtc,
            CanceledAt = message.CanceledAtUtc,
            FailedAt = message.FailedAtUtc,
            LastError = message.LastError,
            PayloadPurgedAt = message.PayloadPurgedAtUtc,
        };
    }

    public static OutboxMessage ToDomain(OutboxMessageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var payload = record.PayloadCiphertext is null
            ? null
            : new EncryptedPayload(
                record.PayloadSchemaVersion ?? throw InvalidPayloadTuple(),
                record.PayloadKeyVersion ?? throw InvalidPayloadTuple(),
                [.. record.PayloadCiphertext]);

        return new OutboxMessage(
            record.EventId,
            record.DedupeKey,
            record.EventType,
            record.AggregateType,
            record.AggregateId,
            record.AggregateVersion,
            record.CorrelationId,
            payload,
            record.CreatedAt,
            record.AvailableAt,
            ParseStatus(record.Status),
            record.Attempts,
            record.LeaseId,
            record.LockedBy,
            record.LockedUntil,
            record.SendingStartedAt,
            record.ProcessedAt,
            record.CanceledAt,
            record.FailedAt,
            record.LastError,
            record.PayloadPurgedAt);
    }

    private static string FormatStatus(OutboxStatus status) => status switch
    {
        OutboxStatus.Pending => "PENDING",
        OutboxStatus.Claimed => "CLAIMED",
        OutboxStatus.Sending => "SENDING",
        OutboxStatus.Processed => "PROCESSED",
        OutboxStatus.DeadLetter => "DEAD_LETTER",
        OutboxStatus.ReviewRequired => "REVIEW_REQUIRED",
        OutboxStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported outbox status."),
    };

    private static OutboxStatus ParseStatus(string status) => status switch
    {
        "PENDING" => OutboxStatus.Pending,
        "CLAIMED" => OutboxStatus.Claimed,
        "SENDING" => OutboxStatus.Sending,
        "PROCESSED" => OutboxStatus.Processed,
        "DEAD_LETTER" => OutboxStatus.DeadLetter,
        "REVIEW_REQUIRED" => OutboxStatus.ReviewRequired,
        "CANCELLED" => OutboxStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unsupported persisted outbox status '{status}'."),
    };

    private static InvalidOperationException InvalidPayloadTuple() =>
        new("Persisted outbox payload metadata is incomplete.");
}
