using DeviceRental.Application.Devices;
using Xunit;

namespace DeviceRental.UnitTests.Devices;

public sealed class DeviceImageUploadPolicyTests
{
    [Theory]
    [MemberData(nameof(SupportedImageHeaders))]
    [Trait("Requirement", "REQ-DEV-006")]
    public async Task ValidateAsync_AcceptsAllowedSignaturesAndReturnsAReReadableContentStream(
        byte[] header,
        string expectedContentType)
    {
        var decoder = new ReadingDecoder(new DeviceImageInspection(1280, 720, 1));
        var policy = new DeviceImageUploadPolicy(decoder);
        var sourceBytes = Append(header, [0x01, 0x02, 0x03, 0x04]);
        await using var source = new MemoryStream(sourceBytes, writable: false);

        var validated = await policy.ValidateAsync(source, TestContext.Current.CancellationToken);

        Assert.Equal(expectedContentType, validated.ContentType);
        Assert.Equal(sourceBytes.LongLength, validated.ByteLength);
        Assert.Equal(1280, validated.PixelWidth);
        Assert.Equal(720, validated.PixelHeight);
        Assert.True(decoder.WasCalled);
        Assert.Equal(0, validated.Content.Position);

        await using var copy = new MemoryStream();
        await validated.Content.CopyToAsync(copy, TestContext.Current.CancellationToken);
        Assert.Equal(sourceBytes, copy.ToArray());
    }

    [Fact]
    [Trait("Requirement", "REQ-DEV-006")]
    public async Task ValidateAsync_AcceptsExactlyFiveMegabytes()
    {
        var policy = new DeviceImageUploadPolicy(new ReadingDecoder(new DeviceImageInspection(1, 1, 1)));
        var sourceBytes = new byte[DeviceImageUploadPolicy.MaximumByteLength];
        PngHeader.CopyTo(sourceBytes, 0);
        await using var source = new MemoryStream(sourceBytes, writable: false);

        var validated = await policy.ValidateAsync(source, TestContext.Current.CancellationToken);

        Assert.Equal(DeviceImageUploadPolicy.MaximumByteLength, validated.ByteLength);
    }

    [Fact]
    [Trait("Requirement", "REQ-DEV-006")]
    public async Task ValidateAsync_RejectsPayloadLargerThanFiveMegabytesBeforeDecoding()
    {
        var decoder = new ReadingDecoder(new DeviceImageInspection(1, 1, 1));
        var policy = new DeviceImageUploadPolicy(decoder);
        var sourceBytes = new byte[DeviceImageUploadPolicy.MaximumByteLength + 1];
        PngHeader.CopyTo(sourceBytes, 0);
        await using var source = new MemoryStream(sourceBytes, writable: false);

        await Assert.ThrowsAsync<ArgumentException>(() => policy.ValidateAsync(source, TestContext.Current.CancellationToken));

        Assert.False(decoder.WasCalled);
    }

    [Theory]
    [MemberData(nameof(DisallowedPayloads))]
    [Trait("Requirement", "REQ-DEV-006")]
    public async Task ValidateAsync_RejectsSvgExecutableAndUnknownPayloads(byte[] sourceBytes)
    {
        var decoder = new ReadingDecoder(new DeviceImageInspection(1, 1, 1));
        var policy = new DeviceImageUploadPolicy(decoder);
        await using var source = new MemoryStream(sourceBytes, writable: false);

        await Assert.ThrowsAsync<ArgumentException>(() => policy.ValidateAsync(source, TestContext.Current.CancellationToken));

        Assert.False(decoder.WasCalled);
    }

    [Theory]
    [InlineData(4097, 1, 1)]
    [InlineData(1, 4097, 1)]
    [InlineData(4096, 4096, 1)]
    [InlineData(100, 100, 2)]
    [Trait("Requirement", "REQ-DEV-006")]
    public async Task ValidateAsync_RejectsOversizedOrAnimatedDecodedImages(int width, int height, int frameCount)
    {
        var policy = new DeviceImageUploadPolicy(new ReadingDecoder(new DeviceImageInspection(width, height, frameCount)));
        await using var source = new MemoryStream(Append(PngHeader, [0x00]), writable: false);

        await Assert.ThrowsAsync<ArgumentException>(() => policy.ValidateAsync(source, TestContext.Current.CancellationToken));
    }

    public static TheoryData<byte[], string> SupportedImageHeaders => new()
    {
        { JpegHeader, "image/jpeg" },
        { PngHeader, "image/png" },
        { WebpHeader, "image/webp" }
    };

    public static TheoryData<byte[]> DisallowedPayloads => new()
    {
        { System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>") },
        { new byte[] { 0x4d, 0x5a, 0x90, 0x00 } },
        { System.Text.Encoding.UTF8.GetBytes("not an image") }
    };

    private static readonly byte[] JpegHeader = [0xff, 0xd8, 0xff, 0xe0];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly byte[] WebpHeader = [0x52, 0x49, 0x46, 0x46, 0x04, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

    private static byte[] Append(byte[] header, byte[] suffix)
    {
        var value = new byte[header.Length + suffix.Length];
        Buffer.BlockCopy(header, 0, value, 0, header.Length);
        Buffer.BlockCopy(suffix, 0, value, header.Length, suffix.Length);
        return value;
    }

    private sealed class ReadingDecoder(DeviceImageInspection inspection) : IDeviceImageDecoder
    {
        public bool WasCalled { get; private set; }

        public async ValueTask<DeviceImageInspection> InspectAsync(
            Stream content,
            DeviceImageDecodeLimits limits,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            await content.CopyToAsync(Stream.Null, cancellationToken);
            return inspection;
        }
    }
}
