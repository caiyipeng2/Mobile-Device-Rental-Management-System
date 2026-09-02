using DeviceRental.Application.Devices;
using SkiaSharp;

namespace DeviceRental.Infrastructure.Images;

public sealed class SkiaSharpDeviceImageDecoder : IDeviceImageDecoder
{
    public ValueTask<DeviceImageInspection> InspectAsync(
        Stream content,
        DeviceImageDecodeLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(limits);
        cancellationToken.ThrowIfCancellationRequested();

        using var codec = SKCodec.Create(content)
            ?? throw new InvalidDataException("The device image could not be decoded.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Width > limits.MaximumDimension ||
            info.Height <= 0 || info.Height > limits.MaximumDimension ||
            (long)info.Width * info.Height > limits.MaximumPixelCount)
        {
            throw new InvalidDataException("The device image exceeds configured decoding limits.");
        }

        if (codec.FrameCount != 1)
        {
            throw new InvalidDataException("Animated device images are not allowed.");
        }

        using var bitmap = new SKBitmap();
        if (!bitmap.TryAllocPixels(info))
        {
            throw new InvalidDataException("The device image could not allocate a decode buffer.");
        }

        var decodeResult = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (decodeResult != SKCodecResult.Success)
        {
            throw new InvalidDataException("The device image could not be fully decoded.");
        }

        return ValueTask.FromResult(new DeviceImageInspection(info.Width, info.Height, codec.FrameCount));
    }
}
