namespace DeviceRental.Infrastructure.Persistence.Records;

public sealed class DeviceImageMetadataRecord
{
    public Guid Id { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long ByteLength { get; set; }

    public int PixelWidth { get; set; }

    public int PixelHeight { get; set; }

    public string Sha256Hex { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
