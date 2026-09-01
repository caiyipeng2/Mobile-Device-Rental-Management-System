using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Notifications;

public enum SmtpAcceptanceEvidence
{
    Accepted,
    NotAccepted,
    Unknown,
}

public enum NotificationChannel
{
    Email,
}

public sealed class EncryptedRecipientAddress
{
    private readonly byte[] _ciphertext;

    public EncryptedRecipientAddress(string keyVersion, byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length == 0)
        {
            throw new ArgumentException("Encrypted recipient address cannot be empty.", nameof(ciphertext));
        }

        KeyVersion = DomainGuard.RequiredText(keyVersion, nameof(keyVersion));
        _ciphertext = [.. ciphertext];
    }

    public string KeyVersion { get; }

    public byte[] Ciphertext => [.. _ciphertext];
}

public sealed class NotificationDelivery
{
    public NotificationDelivery(
        Guid id,
        Guid eventId,
        string deduplicationKey,
        Guid? recipientUserId,
        EncryptedRecipientAddress? encryptedRecipientAddress,
        NotificationChannel channel,
        string templateIdentifier,
        int attemptNumber,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        NotificationSendOutcome outcome,
        SmtpAcceptanceEvidence acceptanceEvidence,
        string? acceptanceEvidenceReference,
        string? sanitizedError)
    {
        Id = DomainGuard.RequiredId(id, nameof(id));
        EventId = DomainGuard.RequiredId(eventId, nameof(eventId));
        DeduplicationKey = DomainGuard.RequiredText(deduplicationKey, nameof(deduplicationKey));
        ValidateRecipient(recipientUserId, encryptedRecipientAddress);
        RecipientUserId = recipientUserId;
        EncryptedRecipientAddress = encryptedRecipientAddress;
        Channel = DomainGuard.DefinedEnum(channel, nameof(channel));
        TemplateIdentifier = DomainGuard.RequiredText(templateIdentifier, nameof(templateIdentifier));

        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), attemptNumber, "Attempt number must be positive.");
        }

        StartedAtUtc = DomainGuard.Utc(startedAtUtc);
        CompletedAtUtc = DomainGuard.Utc(completedAtUtc);
        if (CompletedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAtUtc), completedAtUtc, "Completion cannot precede start.");
        }

        Outcome = DomainGuard.DefinedEnum(outcome, nameof(outcome));
        AcceptanceEvidence = DomainGuard.DefinedEnum(acceptanceEvidence, nameof(acceptanceEvidence));
        _ = DeliveryFailureClassifier.Classify(outcome, acceptanceEvidence);

        if (outcome == NotificationSendOutcome.Accepted)
        {
            AcceptanceEvidenceReference = DomainGuard.RequiredText(
                acceptanceEvidenceReference,
                nameof(acceptanceEvidenceReference));
            if (sanitizedError is not null)
            {
                throw new ArgumentException("An accepted delivery cannot carry an error.", nameof(sanitizedError));
            }
        }
        else
        {
            if (acceptanceEvidenceReference is not null)
            {
                throw new ArgumentException("A failed delivery cannot carry accepted-response evidence.", nameof(acceptanceEvidenceReference));
            }

            SanitizedError = DomainGuard.RequiredText(sanitizedError, nameof(sanitizedError));
        }

        AttemptNumber = attemptNumber;
    }

    public Guid Id { get; }

    public Guid EventId { get; }

    public string DeduplicationKey { get; }

    public Guid? RecipientUserId { get; }

    public EncryptedRecipientAddress? EncryptedRecipientAddress { get; }

    public NotificationChannel Channel { get; }

    public string TemplateIdentifier { get; }

    public int AttemptNumber { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public NotificationSendOutcome Outcome { get; }

    public SmtpAcceptanceEvidence AcceptanceEvidence { get; }

    public string? AcceptanceEvidenceReference { get; }

    public string? SanitizedError { get; }

    private static void ValidateRecipient(
        Guid? recipientUserId,
        EncryptedRecipientAddress? encryptedRecipientAddress)
    {
        if ((recipientUserId is null) == (encryptedRecipientAddress is null))
        {
            throw new ArgumentException("Exactly one recipient representation must be supplied.");
        }

        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Recipient user identifier cannot be empty.", nameof(recipientUserId));
        }
    }
}
