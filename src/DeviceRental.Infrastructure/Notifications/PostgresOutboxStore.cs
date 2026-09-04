using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace DeviceRental.Infrastructure.Notifications;

/// <summary>
/// Persists the short claim/CAS phases for the notification worker. SMTP is intentionally outside
/// this class and must run only after the claim transaction has committed.
/// </summary>
public sealed class PostgresOutboxStore(DeviceRentalDbContext dbContext) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxClaim>> ClaimDueAsync(
        DateTimeOffset effectiveNowUtc,
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = effectiveNowUtc.ToUniversalTime();
        var normalizedWorkerId = workerId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedWorkerId))
        {
            throw new ArgumentException("Worker identifier is required.", nameof(workerId));
        }

        if (batchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be between 1 and 100.");
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }

        var lockedUntil = now.Add(leaseDuration);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var records = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM device_rental.outbox_messages
                WHERE status = 'PENDING' AND available_at <= {now}
                ORDER BY available_at, created_at, event_id
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var claims = new List<OutboxClaim>(records.Count);
        foreach (var record in records)
        {
            var leaseId = Guid.NewGuid();
            record.Status = "CLAIMED";
            record.LeaseId = leaseId;
            record.LockedBy = normalizedWorkerId;
            record.LockedUntil = lockedUntil;
            claims.Add(ToClaim(record, leaseId));
        }

        if (records.Count != 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return claims;
    }

    public async Task<bool> TryStartSendingAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event identifier cannot be empty.", nameof(eventId));
        }

        if (leaseId == Guid.Empty)
        {
            throw new ArgumentException("Lease identifier cannot be empty.", nameof(leaseId));
        }

        var now = effectiveNowUtc.ToUniversalTime();
        var updated = await dbContext.OutboxMessages
            .Where(message =>
                message.EventId == eventId &&
                message.Status == "CLAIMED" &&
                message.LeaseId == leaseId &&
                message.LockedUntil > now &&
                ((message.EventType != "LOAN_ADVANCE_REMINDER" && message.EventType != "LOAN_DUE") ||
                 (message.EventType == "LOAN_DUE" && dbContext.Loans.Any(loan =>
                     loan.Id.ToString() == message.AggregateId &&
                     loan.Version == message.AggregateVersion &&
                     loan.ReturnedAt == null &&
                     loan.DueAt == message.AvailableAt)) ||
                 (message.EventType == "LOAN_ADVANCE_REMINDER" && dbContext.Loans.Any(loan =>
                     loan.Id.ToString() == message.AggregateId &&
                     loan.Version == message.AggregateVersion &&
                     loan.ReturnedAt == null &&
                     loan.DueAt == message.AvailableAt.AddHours(2)))))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, "SENDING")
                .SetProperty(message => message.Attempts, message => message.Attempts + 1)
                .SetProperty(message => message.SendingStartedAt, now)
                .SetProperty(message => message.LastError, (string?)null), cancellationToken);
        return updated == 1;
    }

    public Task<bool> MarkProcessedAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default) =>
        UpdateTerminalAsync(eventId, leaseId, "PROCESSED", effectiveNowUtc, null, cancellationToken);

    public Task<bool> ScheduleRetryAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset availableAtUtc,
        string sanitizedError,
        CancellationToken cancellationToken = default) =>
        UpdateRetryAsync(eventId, leaseId, availableAtUtc, sanitizedError, cancellationToken);

    public Task<bool> MarkFailedAsync(
        Guid eventId,
        Guid leaseId,
        DeviceRental.Domain.Notifications.OutboxStatus status,
        DateTimeOffset effectiveNowUtc,
        string sanitizedError,
        CancellationToken cancellationToken = default) =>
        UpdateTerminalAsync(
            eventId,
            leaseId,
            status switch
            {
                DeviceRental.Domain.Notifications.OutboxStatus.DeadLetter => "DEAD_LETTER",
                DeviceRental.Domain.Notifications.OutboxStatus.ReviewRequired => "REVIEW_REQUIRED",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Only failure states are supported."),
            },
            effectiveNowUtc,
            sanitizedError,
            cancellationToken);

    public async Task<bool> RecordDeliveryAsync(
        OutboxClaim claim,
        NotificationSendResult result,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(result);
        if (result.RecipientUserId is null || string.IsNullOrWhiteSpace(result.TemplateIdentifier))
        {
            return false;
        }

        var delivery = new NotificationDeliveryRecord
        {
            Id = Guid.NewGuid(),
            EventId = claim.EventId,
            DedupeKey = $"{claim.DeduplicationKey}:attempt:{claim.AttemptCount + 1}",
            RecipientUserId = result.RecipientUserId,
            Channel = "EMAIL",
            TemplateIdentifier = result.TemplateIdentifier,
            AttemptNumber = claim.AttemptCount + 1,
            StartedAt = startedAtUtc.ToUniversalTime(),
            CompletedAt = completedAtUtc.ToUniversalTime(),
            Outcome = result.Outcome switch
            {
                DeviceRental.Domain.Notifications.NotificationSendOutcome.Accepted => "ACCEPTED",
                DeviceRental.Domain.Notifications.NotificationSendOutcome.TransientNotAccepted => "TRANSIENT_NOT_ACCEPTED",
                DeviceRental.Domain.Notifications.NotificationSendOutcome.PermanentRejected => "PERMANENT_REJECTED",
                DeviceRental.Domain.Notifications.NotificationSendOutcome.AcceptanceUnknown => "ACCEPTANCE_UNKNOWN",
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unsupported notification outcome."),
            },
            AcceptanceEvidence = result.AcceptanceEvidence switch
            {
                DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence.Accepted => "ACCEPTED",
                DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence.NotAccepted => "NOT_ACCEPTED",
                DeviceRental.Domain.Notifications.SmtpAcceptanceEvidence.Unknown => "UNKNOWN",
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.AcceptanceEvidence, "Unsupported SMTP evidence."),
            },
            AcceptanceEvidenceReference = result.AcceptanceEvidenceReference,
            SanitizedError = result.SanitizedError,
        };

        dbContext.NotificationDeliveries.Add(delivery);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsDeliveryUniqueConflict(exception))
        {
            dbContext.Entry(delivery).State = EntityState.Detached;
            return false;
        }
    }

    private async Task<bool> UpdateTerminalAsync(
        Guid eventId,
        Guid leaseId,
        string status,
        DateTimeOffset effectiveNowUtc,
        string? error,
        CancellationToken cancellationToken)
    {
        var now = effectiveNowUtc.ToUniversalTime();
        var updated = await dbContext.OutboxMessages
            .Where(message => message.EventId == eventId && message.LeaseId == leaseId && message.Status == "SENDING")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, status)
                .SetProperty(message => message.ProcessedAt, status == "PROCESSED" ? now : (DateTimeOffset?)null)
                .SetProperty(message => message.FailedAt, status == "PROCESSED" ? null : now)
                .SetProperty(message => message.LastError, error), cancellationToken);
        return updated == 1;
    }

    private async Task<bool> UpdateRetryAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset availableAtUtc,
        string sanitizedError,
        CancellationToken cancellationToken)
    {
        var updated = await dbContext.OutboxMessages
            .Where(message => message.EventId == eventId && message.LeaseId == leaseId && message.Status == "SENDING")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.Status, "PENDING")
                .SetProperty(message => message.AvailableAt, availableAtUtc.ToUniversalTime())
                .SetProperty(message => message.LeaseId, (Guid?)null)
                .SetProperty(message => message.LockedBy, (string?)null)
                .SetProperty(message => message.LockedUntil, (DateTimeOffset?)null)
                .SetProperty(message => message.SendingStartedAt, (DateTimeOffset?)null)
                .SetProperty(message => message.LastError, sanitizedError), cancellationToken);
        return updated == 1;
    }

    private static OutboxClaim ToClaim(OutboxMessageRecord record, Guid leaseId) =>
        new(
            record.EventId,
            leaseId,
            record.DedupeKey,
            record.EventType,
            record.AggregateType,
            record.AggregateId,
            record.AggregateVersion,
            record.CorrelationId,
            record.Attempts,
            record.AvailableAt,
            record.PayloadSchemaVersion ?? throw new InvalidOperationException("Outbox payload schema version is missing."),
            record.PayloadKeyVersion ?? throw new InvalidOperationException("Outbox payload key version is missing."),
            record.PayloadCiphertext is null
                ? throw new InvalidOperationException("Outbox payload ciphertext is missing.")
                : [.. record.PayloadCiphertext]);

    private static bool IsDeliveryUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_notification_deliveries_event_dedupe",
        };
}
