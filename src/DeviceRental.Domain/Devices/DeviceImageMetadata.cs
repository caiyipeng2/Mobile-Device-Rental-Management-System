using System.Text.RegularExpressions;
using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Devices;

public sealed partial class DeviceImageMetadata
{
    private static readonly HashSet<string> AllowedContentTypes = new(
        ["image/jpeg", "image/png", "image/webp"],
        StringComparer.Ordinal);

    public DeviceImageMetadata(
        Guid id,
        string storageKey,
        string contentType,
        long byteLength,
        int pixelWidth,
        int pixelHeight,
        string sha256Hex,
        DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.RequiredId(id, nameof(id));
        StorageKey = DomainGuard.RequiredText(storageKey, nameof(storageKey));
        ContentType = DomainGuard.RequiredText(contentType, nameof(contentType)).ToLowerInvariant();
        if (!AllowedContentTypes.Contains(ContentType))
        {
            throw new ArgumentException("Unsupported device image content type.", nameof(contentType));
        }

        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), byteLength, "Byte length must be positive.");
        }

        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "Pixel width must be positive.");
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "Pixel height must be positive.");
        }

        var normalizedHash = DomainGuard.RequiredText(sha256Hex, nameof(sha256Hex)).ToLowerInvariant();
        if (!Sha256Pattern().IsMatch(normalizedHash))
        {
            throw new ArgumentException("SHA-256 must contain exactly 64 hexadecimal characters.", nameof(sha256Hex));
        }

        ByteLength = byteLength;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Sha256Hex = normalizedHash;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc);
    }

    public Guid Id { get; }

    public string StorageKey { get; }

    public string ContentType { get; }

    public long ByteLength { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    public string Sha256Hex { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
