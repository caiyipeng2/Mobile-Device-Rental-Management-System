using DeviceRental.Domain.Notifications;
using Xunit;

namespace DeviceRental.UnitTests.Notifications;

public sealed class NotificationEntityInvariantTests
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
    private static readonly Guid LeaseId = Guid.NewGuid();

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public void EncryptedPayload_IsVersionedNonEmptyAndDefensivelyCopied()
    {
        var source = new byte[] { 1, 2, 3 };
        var payload = new EncryptedPayload(2, "key-v3", source);
        source[0] = 9;
        var exposedCopy = payload.Ciphertext;
        exposedCopy[1] = 9;

        Assert.Equal(2, payload.SchemaVersion);
        Assert.Equal("key-v3", payload.KeyVersion);
        Assert.Equal([1, 2, 3], payload.Ciphertext);
        Assert.Throws<ArgumentOutOfRangeException>(() => new EncryptedPayload(0, "key", [1]));
        Assert.Throws<ArgumentException>(() => new EncryptedPayload(1, "key", []));
        Assert.Throws<ArgumentException>(() => new EncryptedPayload(1, " ", [1]));
    }

    [Fact]
    public void OutboxMessage_PendingTupleHasEncryptedPayloadAndNoLeaseOrTerminalTime()
    {
        var message = OutboxMessage.Pending(
            Guid.NewGuid(),
            "loan-1:due",
            "LoanDue",
            "Loan",
            Guid.NewGuid().ToString("D"),
            3,
            "correlation-1",
            Payload(),
            CreatedAt,
            CreatedAt.AddHours(1));

        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal("correlation-1", message.CorrelationId);
        var payload = Assert.IsType<EncryptedPayload>(message.Payload);
        Assert.Equal("key-v1", payload.KeyVersion);
        Assert.Null(message.LeaseId);
        Assert.Null(message.LockedBy);
        Assert.Null(message.LockedUntilUtc);
        Assert.Null(message.SendingStartedAtUtc);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Null(message.CanceledAtUtc);
        Assert.Null(message.FailedAtUtc);
        Assert.Equal(TimeSpan.Zero, message.CreatedAtUtc.Offset);
    }

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-008")]
    public void OutboxMessage_AcceptsTheApprovedStateTruthTable()
    {
        var claimed = CreateOutbox(OutboxStatus.Claimed, 0, withLease: true);
        var sending = CreateOutbox(
            OutboxStatus.Sending,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1));
        var processed = CreateOutbox(
            OutboxStatus.Processed,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            processedAtUtc: CreatedAt.AddMinutes(2));
        var deadLetter = CreateOutbox(
            OutboxStatus.DeadLetter,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            failedAtUtc: CreatedAt.AddMinutes(2),
            lastError: "permanent rejection");
        var review = CreateOutbox(
            OutboxStatus.ReviewRequired,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            failedAtUtc: CreatedAt.AddMinutes(2),
            lastError: "acceptance unknown");
        var cancelledPending = CreateOutbox(
            OutboxStatus.Cancelled,
            0,
            canceledAtUtc: CreatedAt.AddMinutes(1));
        var cancelledClaimed = CreateOutbox(
            OutboxStatus.Cancelled,
            0,
            withLease: true,
            canceledAtUtc: CreatedAt.AddMinutes(1));
        var retryPending = CreateOutbox(
            OutboxStatus.Pending,
            1,
            lastError: "smtp 421 not accepted");

        Assert.Equal(OutboxStatus.Claimed, claimed.Status);
        Assert.Equal(OutboxStatus.Sending, sending.Status);
        Assert.Equal(OutboxStatus.Processed, processed.Status);
        Assert.Equal(OutboxStatus.DeadLetter, deadLetter.Status);
        Assert.Equal(OutboxStatus.ReviewRequired, review.Status);
        Assert.Equal(OutboxStatus.Cancelled, cancelledPending.Status);
        Assert.NotNull(cancelledClaimed.LeaseId);
        Assert.Equal("smtp 421 not accepted", retryPending.LastError);
    }

    [Fact]
    public void OutboxMessage_RejectsEmptyIdentifiersAndPartialOrContradictoryStateTuples()
    {
        Assert.Throws<ArgumentException>(() => OutboxMessage.Pending(
            Guid.Empty,
            "dedupe",
            "type",
            "aggregate",
            "1",
            1,
            "correlation-1",
            Payload(),
            CreatedAt,
            CreatedAt));
        Assert.Throws<ArgumentException>(() => OutboxMessage.Pending(
            Guid.NewGuid(),
            "dedupe",
            "type",
            "aggregate",
            "1",
            1,
            " ",
            Payload(),
            CreatedAt,
            CreatedAt));

        Assert.Throws<ArgumentException>(() => ConstructOutbox(
            OutboxStatus.Claimed,
            0,
            LeaseId,
            null,
            null,
            null,
            null,
            null,
            null,
            null));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Sending,
            1,
            withLease: true));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Processed,
            1,
            withLease: true,
            processedAtUtc: CreatedAt.AddMinutes(2)));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.ReviewRequired,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            failedAtUtc: CreatedAt.AddMinutes(2)));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Pending,
            1));
        Assert.Throws<ArgumentException>(() => ConstructOutbox(
            OutboxStatus.Claimed,
            0,
            Guid.Empty,
            "worker-1",
            CreatedAt.AddMinutes(10),
            null,
            null,
            null,
            null,
            null));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Cancelled,
            0,
            canceledAtUtc: CreatedAt.AddMinutes(1),
            failedAtUtc: CreatedAt.AddMinutes(2)));
        Assert.Throws<ArgumentException>(() => ConstructOutbox(
            OutboxStatus.Pending,
            0,
            null,
            " ",
            null,
            null,
            null,
            null,
            null,
            null));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Pending,
            1,
            lastError: " "));
    }

    [Fact]
    public void OutboxMessage_RejectsSendingBeforeAvailableAt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConstructOutbox(
            OutboxStatus.Sending,
            1,
            LeaseId,
            "worker-1",
            CreatedAt.AddHours(2),
            CreatedAt.AddMinutes(1),
            null,
            null,
            null,
            null,
            availableAtUtc: CreatedAt.AddHours(1)));
    }

    [Theory]
    [InlineData(OutboxStatus.Processed)]
    [InlineData(OutboxStatus.Cancelled)]
    [InlineData(OutboxStatus.DeadLetter)]
    [InlineData(OutboxStatus.ReviewRequired)]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public void OutboxMessage_PurgePayload_PreservesTerminalHistory(OutboxStatus status)
    {
        var terminalMessage = CreateTerminalOutbox(status);
        var terminalAt = TerminalAt(terminalMessage);
        var purgeAt = terminalAt.AddMinutes(1).ToOffset(TimeSpan.FromHours(8));

        var purged = terminalMessage.PurgePayload(purgeAt);

        Assert.NotNull(terminalMessage.Payload);
        Assert.Null(terminalMessage.PayloadPurgedAtUtc);
        Assert.Null(purged.Payload);
        Assert.Equal(terminalAt.AddMinutes(1), purged.PayloadPurgedAtUtc);
        Assert.Equal(TimeSpan.Zero, purged.PayloadPurgedAtUtc!.Value.Offset);
        Assert.Equal(terminalMessage.Id, purged.Id);
        Assert.Equal(terminalMessage.DeduplicationKey, purged.DeduplicationKey);
        Assert.Equal(terminalMessage.Status, purged.Status);
    }

    [Theory]
    [InlineData(OutboxStatus.Pending)]
    [InlineData(OutboxStatus.Claimed)]
    [InlineData(OutboxStatus.Sending)]
    public void OutboxMessage_PurgePayload_RejectsNonterminalStates(OutboxStatus status)
    {
        var message = status switch
        {
            OutboxStatus.Pending => CreateOutbox(status, 0),
            OutboxStatus.Claimed => CreateOutbox(status, 0, withLease: true),
            OutboxStatus.Sending => CreateOutbox(
                status,
                1,
                withLease: true,
                sendingStartedAtUtc: CreatedAt.AddMinutes(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        Assert.Throws<InvalidOperationException>(() => message.PurgePayload(CreatedAt.AddHours(1)));
    }

    [Fact]
    public void OutboxMessage_PurgePayload_RejectsEarlyAndRepeatedPurge()
    {
        var processed = CreateTerminalOutbox(OutboxStatus.Processed);
        var terminalAt = TerminalAt(processed);

        Assert.Throws<ArgumentOutOfRangeException>(() => processed.PurgePayload(terminalAt.AddTicks(-1)));

        var purged = processed.PurgePayload(terminalAt);
        Assert.Throws<InvalidOperationException>(() => purged.PurgePayload(terminalAt.AddMinutes(1)));
    }

    [Fact]
    public void OutboxMessage_Constructor_EnforcesPayloadAndPurgeTimestampExclusivity()
    {
        var terminalAt = CreatedAt.AddMinutes(2);

        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Processed,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            processedAtUtc: terminalAt,
            includePayload: false));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Processed,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            processedAtUtc: terminalAt,
            payloadPurgedAtUtc: terminalAt.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Pending,
            0,
            includePayload: false));
        Assert.Throws<ArgumentException>(() => CreateOutbox(
            OutboxStatus.Pending,
            0,
            payloadPurgedAtUtc: CreatedAt.AddMinutes(1)));
    }

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public void NotificationDelivery_UserRecipientIncludesDedupeChannelAndTemplate()
    {
        var recipientUserId = Guid.NewGuid();
        var accepted = CreateDelivery(
            recipientUserId,
            null,
            NotificationSendOutcome.Accepted,
            SmtpAcceptanceEvidence.Accepted,
            "smtp-250-response",
            null);

        Assert.Equal(recipientUserId, accepted.RecipientUserId);
        Assert.Null(accepted.EncryptedRecipientAddress);
        Assert.Equal("delivery-dedupe-1", accepted.DeduplicationKey);
        Assert.Equal(NotificationChannel.Email, accepted.Channel);
        Assert.Equal("loan-due-v1", accepted.TemplateIdentifier);
        Assert.Equal(TimeSpan.Zero, accepted.CompletedAtUtc.Offset);
        Assert.Equal("smtp-250-response", accepted.AcceptanceEvidenceReference);
    }

    [Fact]
    public void EncryptedRecipientAddress_IsNonEmptyVersionedCiphertextAndDefensivelyCopied()
    {
        var source = new byte[] { 4, 5, 6 };
        var encryptedAddress = new EncryptedRecipientAddress("key-v2", source);
        source[0] = 9;
        var exposedCopy = encryptedAddress.Ciphertext;
        exposedCopy[1] = 9;

        Assert.Equal("key-v2", encryptedAddress.KeyVersion);
        Assert.Equal([4, 5, 6], encryptedAddress.Ciphertext);
        Assert.Throws<ArgumentException>(() => new EncryptedRecipientAddress("key", []));
    }

    [Theory]
    [InlineData(NotificationSendOutcome.TransientNotAccepted, SmtpAcceptanceEvidence.NotAccepted)]
    [InlineData(NotificationSendOutcome.PermanentRejected, SmtpAcceptanceEvidence.NotAccepted)]
    [InlineData(NotificationSendOutcome.AcceptanceUnknown, SmtpAcceptanceEvidence.Unknown)]
    public void NotificationDelivery_EncryptedRecipientAcceptsExplicitFailureEvidence(
        NotificationSendOutcome outcome,
        SmtpAcceptanceEvidence evidence)
    {
        var delivery = CreateDelivery(
            null,
            EncryptedAddress(),
            outcome,
            evidence,
            null,
            "sanitized failure");

        Assert.Null(delivery.RecipientUserId);
        Assert.NotNull(delivery.EncryptedRecipientAddress);
        Assert.Equal(outcome, delivery.Outcome);
        Assert.Equal(evidence, delivery.AcceptanceEvidence);
        Assert.Equal("sanitized failure", delivery.SanitizedError);
    }

    [Fact]
    public void NotificationDelivery_RequiresExactlyOneNonEmptyRecipientRepresentation()
    {
        Assert.Throws<ArgumentException>(() => CreateDelivery(
            null,
            null,
            NotificationSendOutcome.Accepted,
            SmtpAcceptanceEvidence.Accepted,
            "accepted",
            null));
        Assert.Throws<ArgumentException>(() => CreateDelivery(
            Guid.NewGuid(),
            EncryptedAddress(),
            NotificationSendOutcome.Accepted,
            SmtpAcceptanceEvidence.Accepted,
            "accepted",
            null));
        Assert.Throws<ArgumentException>(() => CreateDelivery(
            Guid.Empty,
            null,
            NotificationSendOutcome.Accepted,
            SmtpAcceptanceEvidence.Accepted,
            "accepted",
            null));
    }

    [Fact]
    public void NotificationDelivery_EnforcesOutcomeEvidenceAndCompletionTuple()
    {
        Assert.Throws<ArgumentException>(() => CreateDelivery(
            Guid.NewGuid(),
            null,
            NotificationSendOutcome.Accepted,
            SmtpAcceptanceEvidence.Accepted,
            null,
            null));
        Assert.Throws<ArgumentException>(() => CreateDelivery(
            Guid.NewGuid(),
            null,
            NotificationSendOutcome.AcceptanceUnknown,
            SmtpAcceptanceEvidence.Unknown,
            null,
            null));
        Assert.Throws<ArgumentException>(() => CreateDelivery(
            Guid.NewGuid(),
            null,
            NotificationSendOutcome.AcceptanceUnknown,
            SmtpAcceptanceEvidence.NotAccepted,
            null,
            "failure"));
    }

    private static EncryptedPayload Payload() => new(1, "key-v1", [1, 2, 3]);

    private static EncryptedRecipientAddress EncryptedAddress() => new("key-v1", [4, 5, 6]);

    private static OutboxMessage CreateTerminalOutbox(OutboxStatus status) => status switch
    {
        OutboxStatus.Processed => CreateOutbox(
            status,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            processedAtUtc: CreatedAt.AddMinutes(2)),
        OutboxStatus.Cancelled => CreateOutbox(
            status,
            0,
            canceledAtUtc: CreatedAt.AddMinutes(2)),
        OutboxStatus.DeadLetter or OutboxStatus.ReviewRequired => CreateOutbox(
            status,
            1,
            withLease: true,
            sendingStartedAtUtc: CreatedAt.AddMinutes(1),
            failedAtUtc: CreatedAt.AddMinutes(2),
            lastError: "sanitized failure"),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static DateTimeOffset TerminalAt(OutboxMessage message) =>
        message.ProcessedAtUtc ?? message.CanceledAtUtc ?? message.FailedAtUtc ??
        throw new InvalidOperationException("Expected a terminal message.");

    private static OutboxMessage CreateOutbox(
        OutboxStatus status,
        int attemptCount,
        bool withLease = false,
        DateTimeOffset? sendingStartedAtUtc = null,
        DateTimeOffset? processedAtUtc = null,
        DateTimeOffset? canceledAtUtc = null,
        DateTimeOffset? failedAtUtc = null,
        string? lastError = null,
        bool includePayload = true,
        DateTimeOffset? payloadPurgedAtUtc = null) =>
        ConstructOutbox(
            status,
            attemptCount,
            withLease ? LeaseId : null,
            withLease ? "worker-1" : null,
            withLease ? CreatedAt.AddMinutes(10) : null,
            sendingStartedAtUtc,
            processedAtUtc,
            canceledAtUtc,
            failedAtUtc,
            lastError,
            payloadPurgedAtUtc: payloadPurgedAtUtc,
            includePayload: includePayload);

    private static OutboxMessage ConstructOutbox(
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
        DateTimeOffset? availableAtUtc = null,
        DateTimeOffset? payloadPurgedAtUtc = null,
        bool includePayload = true) =>
        new(
            Guid.NewGuid(),
            $"dedupe-{status}",
            "type",
            "aggregate",
            "1",
            1,
            "correlation-1",
            includePayload ? Payload() : null,
            CreatedAt,
            availableAtUtc ?? CreatedAt,
            status,
            attemptCount,
            leaseId,
            lockedBy,
            lockedUntilUtc,
            sendingStartedAtUtc,
            processedAtUtc,
            canceledAtUtc,
            failedAtUtc,
            lastError,
            payloadPurgedAtUtc);

    private static NotificationDelivery CreateDelivery(
        Guid? recipientUserId,
        EncryptedRecipientAddress? encryptedRecipientAddress,
        NotificationSendOutcome outcome,
        SmtpAcceptanceEvidence evidence,
        string? acceptanceEvidenceReference,
        string? sanitizedError) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "delivery-dedupe-1",
            recipientUserId,
            encryptedRecipientAddress,
            NotificationChannel.Email,
            "loan-due-v1",
            1,
            CreatedAt,
            CreatedAt.AddSeconds(1),
            outcome,
            evidence,
            acceptanceEvidenceReference,
            sanitizedError);
}
