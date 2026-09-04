namespace DeviceRental.Infrastructure.Persistence.Records;

public sealed class NotificationDeliveryRecord
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public Guid? RecipientUserId { get; set; }

    public string? RecipientKeyVersion { get; set; }

    public byte[]? RecipientCiphertext { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string TemplateIdentifier { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string AcceptanceEvidence { get; set; } = string.Empty;

    public string? AcceptanceEvidenceReference { get; set; }

    public string? SanitizedError { get; set; }
}
