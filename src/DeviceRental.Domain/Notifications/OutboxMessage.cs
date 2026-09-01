using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Notifications;

public sealed class EncryptedPayload
{
    private readonly byte[] _ciphertext;

    public EncryptedPayload(int schemaVersion, string keyVersion, byte[] ciphertext)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Schema version must be positive.");
        }

        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length == 0)
        {
            throw new ArgumentException("Encrypted payload cannot be empty.", nameof(ciphertext));
        }

        SchemaVersion = schemaVersion;
        KeyVersion = DomainGuard.RequiredText(keyVersion, nameof(keyVersion));
        _ciphertext = [.. ciphertext];
    }

    public int SchemaVersion { get; }

    public string KeyVersion { get; }

    // A copy prevents callers from mutating encrypted persistence data after validation.
    public byte[] Ciphertext => [.. _ciphertext];
}

public sealed class OutboxMessage
{
    public OutboxMessage(
        Guid id,
        string deduplicationKey,
        string messageType,
        string aggregateType,
        string aggregateId,
        long aggregateVersion,
        EncryptedPayload? payload,
        DateTimeOffset createdAtUtc,
        DateTimeOffset availableAtUtc,
        OutboxStatus status,
        int attemptCount,
        Guid? leaseId,
        string? lockedBy,
        DateTimeOffset? lockedUntilUtc,
        DateTimeOffset? sendingStartedAtUtc,
        DateTimeOffset? processedAtUtc,
        DateTimeOffset? canceledAtUtc,
        DateTimeOffset? failedAtUtc,
        string? lastError,
        DateTimeOffset? payloadPurgedAtUtc = null)
    {
        Id = DomainGuard.RequiredId(id, nameof(id));
        DeduplicationKey = DomainGuard.RequiredText(deduplicationKey, nameof(deduplicationKey));
        MessageType = DomainGuard.RequiredText(messageType, nameof(messageType));
        AggregateType = DomainGuard.RequiredText(aggregateType, nameof(aggregateType));
        AggregateId = DomainGuard.RequiredText(aggregateId, nameof(aggregateId));
        if (aggregateVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aggregateVersion), aggregateVersion, "Aggregate version must be positive.");
        }

        CreatedAtUtc = DomainGuard.Utc(createdAtUtc);
        AvailableAtUtc = DomainGuard.Utc(availableAtUtc);
        if (AvailableAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(availableAtUtc), availableAtUtc, "Availability cannot precede creation.");
        }

        Status = DomainGuard.DefinedEnum(status, nameof(status));
        if (attemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptCount), attemptCount, "Attempt count cannot be negative.");
        }

        LeaseId = leaseId;
        LockedBy = lockedBy is null ? null : DomainGuard.RequiredText(lockedBy, nameof(lockedBy));
        LockedUntilUtc = Normalize(lockedUntilUtc);
        SendingStartedAtUtc = Normalize(sendingStartedAtUtc);
        ProcessedAtUtc = Normalize(processedAtUtc);
        CanceledAtUtc = Normalize(canceledAtUtc);
        FailedAtUtc = Normalize(failedAtUtc);
        PayloadPurgedAtUtc = Normalize(payloadPurgedAtUtc);
        LastError = lastError is null ? null : DomainGuard.RequiredText(lastError, nameof(lastError));
        AggregateVersion = aggregateVersion;
        Payload = payload;
        AttemptCount = attemptCount;

        ValidateStateTuple(attemptCount);
    }

    public Guid Id { get; }

    public string DeduplicationKey { get; }

    public string MessageType { get; }

    public string AggregateType { get; }

    public string AggregateId { get; }

    public long AggregateVersion { get; }

    public EncryptedPayload? Payload { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset AvailableAtUtc { get; }

    public OutboxStatus Status { get; }

    public int AttemptCount { get; }

    public Guid? LeaseId { get; }

    public string? LockedBy { get; }

    public DateTimeOffset? LockedUntilUtc { get; }

    public DateTimeOffset? SendingStartedAtUtc { get; }

    public DateTimeOffset? ProcessedAtUtc { get; }

    public DateTimeOffset? CanceledAtUtc { get; }

    public DateTimeOffset? FailedAtUtc { get; }

    public DateTimeOffset? PayloadPurgedAtUtc { get; }

    public string? LastError { get; }

    public static OutboxMessage Pending(
        Guid id,
        string deduplicationKey,
        string messageType,
        string aggregateType,
        string aggregateId,
        long aggregateVersion,
        EncryptedPayload payload,
        DateTimeOffset createdAtUtc,
        DateTimeOffset availableAtUtc) =>
        new(
            id,
            deduplicationKey,
            messageType,
            aggregateType,
            aggregateId,
            aggregateVersion,
            payload,
            createdAtUtc,
            availableAtUtc,
            OutboxStatus.Pending,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    public OutboxMessage PurgePayload(DateTimeOffset purgedAtUtc)
    {
        if (!IsTerminal(Status))
        {
            throw new InvalidOperationException("Payload can be purged only after the message reaches a terminal state.");
        }

        if (Payload is null || PayloadPurgedAtUtc is not null)
        {
            throw new InvalidOperationException("Payload has already been purged.");
        }

        return new OutboxMessage(
            Id,
            DeduplicationKey,
            MessageType,
            AggregateType,
            AggregateId,
            AggregateVersion,
            null,
            CreatedAtUtc,
            AvailableAtUtc,
            Status,
            AttemptCount,
            LeaseId,
            LockedBy,
            LockedUntilUtc,
            SendingStartedAtUtc,
            ProcessedAtUtc,
            CanceledAtUtc,
            FailedAtUtc,
            LastError,
            purgedAtUtc);
    }

    private static DateTimeOffset? Normalize(DateTimeOffset? value) =>
        value is null ? null : DomainGuard.Utc(value.Value);

    private void ValidateStateTuple(int attemptCount)
    {
        var hasLeaseId = LeaseId is not null;
        var hasLockedBy = !string.IsNullOrWhiteSpace(LockedBy);
        var hasLockedUntil = LockedUntilUtc is not null;
        var hasCompleteLease = hasLeaseId && hasLockedBy && hasLockedUntil;
        if ((hasLeaseId || hasLockedBy || hasLockedUntil) && !hasCompleteLease)
        {
            throw new ArgumentException("Lease id, lock owner, and lock expiry must be supplied together.");
        }

        if (LeaseId == Guid.Empty)
        {
            throw new ArgumentException("Lease identifier cannot be empty.", nameof(LeaseId));
        }

        if (LockedUntilUtc is not null && LockedUntilUtc <= CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(LockedUntilUtc), LockedUntilUtc, "Lock expiry must follow creation.");
        }

        EnsureNotBeforeCreation(SendingStartedAtUtc, nameof(SendingStartedAtUtc));
        EnsureNotBeforeCreation(ProcessedAtUtc, nameof(ProcessedAtUtc));
        EnsureNotBeforeCreation(CanceledAtUtc, nameof(CanceledAtUtc));
        EnsureNotBeforeCreation(FailedAtUtc, nameof(FailedAtUtc));
        EnsureNotBeforeCreation(PayloadPurgedAtUtc, nameof(PayloadPurgedAtUtc));
        if (SendingStartedAtUtc is not null && SendingStartedAtUtc < AvailableAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SendingStartedAtUtc),
                SendingStartedAtUtc,
                "Sending cannot begin before the message is available.");
        }

        var hasSending = SendingStartedAtUtc is not null;
        var hasProcessed = ProcessedAtUtc is not null;
        var hasCanceled = CanceledAtUtc is not null;
        var hasFailed = FailedAtUtc is not null;
        var hasError = !string.IsNullOrWhiteSpace(LastError);
        var terminalCount = (hasProcessed ? 1 : 0) + (hasCanceled ? 1 : 0) + (hasFailed ? 1 : 0);
        if (terminalCount > 1)
        {
            throw new ArgumentException("Only one terminal timestamp may be populated.");
        }

        switch (Status)
        {
            case OutboxStatus.Pending
                when hasCompleteLease || hasSending || terminalCount != 0 || hasError != (attemptCount > 0):
                throw new ArgumentException("Pending state has an inconsistent retry, lease, or timestamp tuple.");
            case OutboxStatus.Claimed
                when !hasCompleteLease || hasSending || terminalCount != 0 || hasError != (attemptCount > 0):
                throw new ArgumentException("Claimed state requires only a complete lease tuple.");
            case OutboxStatus.Sending
                when attemptCount == 0 || !hasCompleteLease || !hasSending || terminalCount != 0 || hasError:
                throw new ArgumentException("Sending state requires a lease and sending time without terminal state.");
            case OutboxStatus.Processed
                when attemptCount == 0 || !hasCompleteLease || !hasSending || !hasProcessed || hasCanceled || hasFailed || hasError:
                throw new ArgumentException("Processed state requires lease, sending, and processed timestamps only.");
            case OutboxStatus.DeadLetter or OutboxStatus.ReviewRequired
                when attemptCount == 0 || !hasCompleteLease || !hasSending || !hasFailed || hasProcessed || hasCanceled || !hasError:
                throw new ArgumentException("Failure state requires lease, sending, failure time, and sanitized error.");
            case OutboxStatus.Cancelled
                when hasSending || !hasCanceled || hasProcessed || hasFailed || hasError:
                throw new ArgumentException("Cancelled state requires only a cancellation time and an optional complete lease.");
        }

        if (hasSending && SendingStartedAtUtc >= LockedUntilUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(SendingStartedAtUtc), SendingStartedAtUtc, "Sending must begin within the claimed lease.");
        }

        var terminalAt = ProcessedAtUtc ?? FailedAtUtc;
        if (terminalAt is not null && terminalAt < SendingStartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(terminalAt), terminalAt, "Terminal time cannot precede sending.");
        }

        ValidatePayloadRetention();
    }

    private void ValidatePayloadRetention()
    {
        if (!IsTerminal(Status))
        {
            if (Payload is null || PayloadPurgedAtUtc is not null)
            {
                throw new ArgumentException("A nonterminal message must retain its encrypted payload without a purge time.");
            }

            return;
        }

        if ((Payload is null) == (PayloadPurgedAtUtc is null))
        {
            throw new ArgumentException("A terminal message must have exactly one of payload or payload-purge time.");
        }

        var terminalAt = ProcessedAtUtc ?? CanceledAtUtc ?? FailedAtUtc ??
            throw new InvalidOperationException("A terminal message requires a terminal timestamp.");
        if (PayloadPurgedAtUtc is not null && PayloadPurgedAtUtc < terminalAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PayloadPurgedAtUtc),
                PayloadPurgedAtUtc,
                "Payload cannot be purged before the terminal transition.");
        }
    }

    private static bool IsTerminal(OutboxStatus status) => status is
        OutboxStatus.Processed or
        OutboxStatus.Cancelled or
        OutboxStatus.DeadLetter or
        OutboxStatus.ReviewRequired;

    private void EnsureNotBeforeCreation(DateTimeOffset? value, string parameterName)
    {
        if (value is not null && value < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Timestamp cannot precede creation.");
        }
    }
}
