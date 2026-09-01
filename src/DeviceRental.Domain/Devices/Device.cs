using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;

namespace DeviceRental.Domain.Devices;

public enum DeviceAvailability
{
    Available,
    Borrowed,
    Unavailable,
}

public sealed class Device
{
    public Device(
        Guid id,
        string assetNumber,
        string modelName,
        DeviceTier tier,
        Guid imageId,
        ManualDeviceState manualState = ManualDeviceState.Normal,
        Reason? temporaryUnavailableReason = null,
        bool isArchived = false)
    {
        Id = DomainGuard.RequiredId(id, nameof(id));
        AssetNumber = DomainGuard.RequiredText(assetNumber, nameof(assetNumber));
        ModelName = DomainGuard.RequiredText(modelName, nameof(modelName));
        Tier = DomainGuard.DefinedEnum(tier, nameof(tier));
        ImageId = DomainGuard.RequiredId(imageId, nameof(imageId));
        ManualState = DomainGuard.DefinedEnum(manualState, nameof(manualState));

        if (manualState == ManualDeviceState.TemporarilyDisabled && temporaryUnavailableReason is null)
        {
            throw new ArgumentException(
                "A temporarily disabled device requires a reason.",
                nameof(temporaryUnavailableReason));
        }

        if (manualState == ManualDeviceState.Normal && temporaryUnavailableReason is not null)
        {
            throw new ArgumentException(
                "A normal device cannot retain a temporary-unavailability reason.",
                nameof(temporaryUnavailableReason));
        }

        TemporaryUnavailableReason = temporaryUnavailableReason;
        IsArchived = isArchived;
    }

    public Guid Id { get; }

    public string AssetNumber { get; }

    public string ModelName { get; }

    public DeviceTier Tier { get; }

    public Guid ImageId { get; }

    public ManualDeviceState ManualState { get; }

    public Reason? TemporaryUnavailableReason { get; }

    // Archival is lifecycle metadata and never a fourth availability state.
    public bool IsArchived { get; }

    public DeviceAvailability GetAvailability(Loan? openLoan)
    {
        if (openLoan is not null)
        {
            if (!openLoan.IsOpen || openLoan.DeviceId != Id)
            {
                throw new ArgumentException(
                    "The supplied loan must be an open loan for this device.",
                    nameof(openLoan));
            }

            return DeviceAvailability.Borrowed;
        }

        return ManualState == ManualDeviceState.TemporarilyDisabled
            ? DeviceAvailability.Unavailable
            : DeviceAvailability.Available;
    }

    public bool IsBorrowable(Loan? openLoan) =>
        !IsArchived && GetAvailability(openLoan) == DeviceAvailability.Available;
}
