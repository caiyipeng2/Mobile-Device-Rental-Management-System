namespace DeviceRental.Application.Notifications;

public sealed record OutboxClaim(
    Guid EventId,
    Guid LeaseId,
    string EventType,
    string AggregateType,
    string AggregateId,
    long AggregateVersion,
    string CorrelationId,
    int AttemptCount,
    DateTimeOffset AvailableAtUtc,
    int PayloadSchemaVersion,
    string PayloadKeyVersion,
    byte[] PayloadCiphertext);

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxClaim>> ClaimDueAsync(
        DateTimeOffset effectiveNowUtc,
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task<bool> TryStartSendingAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> MarkProcessedAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> ScheduleRetryAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset availableAtUtc,
        string sanitizedError,
        CancellationToken cancellationToken = default);

    Task<bool> MarkFailedAsync(
        Guid eventId,
        Guid leaseId,
        DeviceRental.Domain.Notifications.OutboxStatus status,
        DateTimeOffset effectiveNowUtc,
        string sanitizedError,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationSendResult(
    DeviceRental.Domain.Notifications.NotificationSendOutcome Outcome,
    DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence AcceptanceEvidence,
    string? AcceptanceEvidenceReference,
    string? SanitizedError)
{
    public static NotificationSendResult Accepted(string evidenceReference) =>
        new(DeviceRental.Domain.Notifications.NotificationSendOutcome.Accepted,
            DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence.Accepted,
            evidenceReference,
            null);

    public static NotificationSendResult TransientFailure(string error) =>
        new(DeviceRental.Domain.Notifications.NotificationSendOutcome.TransientNotAccepted,
            DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence.NotAccepted,
            null,
            error);

    public static NotificationSendResult PermanentFailure(string error) =>
        new(DeviceRental.Domain.Notifications.NotificationSendOutcome.PermanentRejected,
            DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence.NotAccepted,
            null,
            error);

    public static NotificationSendResult AcceptanceUnknown(string error) =>
        new(DeviceRental.Domain.Notifications.NotificationSendOutcome.AcceptanceUnknown,
            DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence.Unknown,
            null,
            error);
}

public interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(
        OutboxClaim claim,
        CancellationToken cancellationToken = default);
}

public interface INotificationPayloadCodec
{
    byte[] Encode(NotificationPayload payload, int schemaVersion);

    NotificationPayload Decode(OutboxClaim claim);
}
