using DeviceRental.Domain.Devices;

namespace DeviceRental.Application.Devices;

public interface IDeviceRegistrationStore
{
    Task<DeviceRegistrationStoreResult> RegisterAsync(
        Device device,
        DeviceImageMetadata image,
        CancellationToken cancellationToken = default);
}

public enum DeviceRegistrationStoreStatus
{
    Succeeded,
    DuplicateAssetNumber,
}

public sealed record DeviceRegistrationStoreResult(DeviceRegistrationStoreStatus Status);
