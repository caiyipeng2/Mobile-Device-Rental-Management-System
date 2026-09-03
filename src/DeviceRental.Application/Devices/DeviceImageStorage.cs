namespace DeviceRental.Application.Devices;

public sealed record StoredDeviceImage(
    Guid Id,
    string StorageKey,
    string ContentType,
    long ByteLength,
    int PixelWidth,
    int PixelHeight,
    string Sha256Hex);

public interface IDeviceImageStorage
{
    Task<StoredDeviceImage> SaveAsync(
        Guid imageId,
        ValidatedDeviceImage image,
        CancellationToken cancellationToken = default);

    ValueTask<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
