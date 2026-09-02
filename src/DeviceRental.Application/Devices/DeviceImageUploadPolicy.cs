using System.Security.Cryptography;

namespace DeviceRental.Application.Devices;

public sealed record DeviceImageInspection(int PixelWidth, int PixelHeight, int FrameCount);

public sealed record DeviceImageDecodeLimits(int MaximumDimension, long MaximumPixelCount);

public interface IDeviceImageDecoder
{
    ValueTask<DeviceImageInspection> InspectAsync(
        Stream content,
        DeviceImageDecodeLimits limits,
        CancellationToken cancellationToken = default);
}

public sealed record ValidatedDeviceImage(
    Stream Content,
    string ContentType,
    long ByteLength,
    int PixelWidth,
    int PixelHeight,
    string Sha256Hex);

public sealed class DeviceImageUploadPolicy
{
    public const int MaximumByteLength = 5 * 1024 * 1024;
    public const int MaximumDimension = 4096;
    public const long MaximumPixelCount = 16_000_000L;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly DeviceImageDecodeLimits DecodeLimits = new(MaximumDimension, MaximumPixelCount);
    private readonly IDeviceImageDecoder _decoder;

    public DeviceImageUploadPolicy(IDeviceImageDecoder decoder)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
    }

    public async Task<ValidatedDeviceImage> ValidateAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new ArgumentException("Device image content must be readable.", nameof(content));
        }

        var bufferedContent = await ReadBoundedCopyAsync(content, cancellationToken);
        try
        {
            var contentType = GetContentType(bufferedContent.GetBuffer().AsSpan(0, checked((int)bufferedContent.Length)));
            bufferedContent.Position = 0;
            DeviceImageInspection inspection;
            try
            {
                inspection = await _decoder.InspectAsync(bufferedContent, DecodeLimits, cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                throw new ArgumentException("Device image content could not be fully decoded.", nameof(content), exception);
            }

            ValidateInspection(inspection);

            var hash = Convert.ToHexString(SHA256.HashData(
                bufferedContent.GetBuffer().AsSpan(0, checked((int)bufferedContent.Length)))).ToLowerInvariant();
            bufferedContent.Position = 0;
            return new ValidatedDeviceImage(
                bufferedContent,
                contentType,
                bufferedContent.Length,
                inspection.PixelWidth,
                inspection.PixelHeight,
                hash);
        }
        catch
        {
            await bufferedContent.DisposeAsync();
            throw;
        }
    }

    private static async ValueTask<MemoryStream> ReadBoundedCopyAsync(Stream content, CancellationToken cancellationToken)
    {
        var bufferedContent = new MemoryStream();
        var buffer = new byte[81920];
        try
        {
            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                if (bufferedContent.Length > MaximumByteLength - read)
                {
                    throw new ArgumentException("Device image content exceeds the 5 MB limit.", nameof(content));
                }

                await bufferedContent.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (bufferedContent.Length == 0)
            {
                throw new ArgumentException("Device image content cannot be empty.", nameof(content));
            }

            return bufferedContent;
        }
        catch
        {
            await bufferedContent.DisposeAsync();
            throw;
        }
    }

    private static string GetContentType(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff)
        {
            return "image/jpeg";
        }

        if (content.Length >= PngSignature.Length && content[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return "image/png";
        }

        if (content.Length >= 12 &&
            content[..4].SequenceEqual("RIFF"u8) &&
            content.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        throw new ArgumentException("Only JPEG, PNG, and WebP device images are allowed.", nameof(content));
    }

    private static void ValidateInspection(DeviceImageInspection inspection)
    {
        if (inspection.PixelWidth is <= 0 or > MaximumDimension ||
            inspection.PixelHeight is <= 0 or > MaximumDimension)
        {
            throw new ArgumentException("Device image dimensions exceed the allowed limit.", nameof(inspection));
        }

        if ((long)inspection.PixelWidth * inspection.PixelHeight > MaximumPixelCount)
        {
            throw new ArgumentException("Device image pixel count exceeds the allowed limit.", nameof(inspection));
        }

        if (inspection.FrameCount != 1)
        {
            throw new ArgumentException("Animated device images are not allowed.", nameof(inspection));
        }
    }
}
