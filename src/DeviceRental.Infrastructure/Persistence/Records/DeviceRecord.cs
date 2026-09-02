namespace DeviceRental.Infrastructure.Persistence.Records;

public sealed class DeviceRecord
{
    public Guid Id { get; set; }

    public string AssetNumber { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string Tier { get; set; } = string.Empty;

    public Guid ImageId { get; set; }

    public string ManualState { get; set; } = "NORMAL";

    public string? TemporaryUnavailableReason { get; set; }

    public bool IsArchived { get; set; }

    public long Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
