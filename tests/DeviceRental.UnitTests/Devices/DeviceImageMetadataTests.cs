using DeviceRental.Domain.Devices;
using Xunit;

namespace DeviceRental.UnitTests.Devices;

public sealed class DeviceImageMetadataTests
{
    [Fact]
    public void Constructor_NormalizesMetadataAndUtcTimestamp()
    {
        var metadata = new DeviceImageMetadata(
            Guid.NewGuid(),
            " devices/image.webp ",
            "IMAGE/WEBP",
            1234,
            640,
            480,
            new string('A', 64),
            new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(8)));

        Assert.Equal("devices/image.webp", metadata.StorageKey);
        Assert.Equal("image/webp", metadata.ContentType);
        Assert.Equal(new string('a', 64), metadata.Sha256Hex);
        Assert.Equal(TimeSpan.Zero, metadata.CreatedAtUtc.Offset);
    }

    [Theory]
    [InlineData("image/gif", 10, 10, 10)]
    [InlineData("image/png", 0, 10, 10)]
    [InlineData("image/png", 10, 0, 10)]
    [InlineData("image/png", 10, 10, 0)]
    public void Constructor_RejectsInvalidContentOrDimensions(
        string contentType,
        long bytes,
        int width,
        int height)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DeviceImageMetadata(
            Guid.NewGuid(),
            "key",
            contentType,
            bytes,
            width,
            height,
            new string('a', 64),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsEmptyIdKeyAndInvalidSha256()
    {
        Assert.Throws<ArgumentException>(() => new DeviceImageMetadata(
            Guid.Empty, "key", "image/jpeg", 1, 1, 1, new string('a', 64), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new DeviceImageMetadata(
            Guid.NewGuid(), " ", "image/jpeg", 1, 1, 1, new string('a', 64), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new DeviceImageMetadata(
            Guid.NewGuid(), "key", "image/jpeg", 1, 1, 1, "not-a-hash", DateTimeOffset.UtcNow));
    }
}
