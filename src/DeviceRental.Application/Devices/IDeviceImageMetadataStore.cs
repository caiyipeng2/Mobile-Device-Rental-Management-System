using DeviceRental.Domain.Devices;

namespace DeviceRental.Application.Devices;

public interface IDeviceImageMetadataStore
{
    Task SaveAsync(
        DeviceImageMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<DeviceImageMetadata?> FindAsync(
        Guid imageId,
        CancellationToken cancellationToken = default);
}
