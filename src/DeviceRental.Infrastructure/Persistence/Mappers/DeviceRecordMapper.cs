using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Infrastructure.Persistence.Records;

namespace DeviceRental.Infrastructure.Persistence.Mappers;

public static class DeviceRecordMapper
{
    public static DeviceRecord ToRecord(
        Device device,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        long version = 1)
    {
        ArgumentNullException.ThrowIfNull(device);
        return new DeviceRecord
        {
            Id = device.Id,
            AssetNumber = device.AssetNumber,
            ModelName = device.ModelName,
            Tier = device.Tier switch
            {
                DeviceTier.Low => "LOW",
                DeviceTier.Mid => "MID",
                DeviceTier.High => "HIGH",
                _ => throw new ArgumentOutOfRangeException(nameof(device), device.Tier, "Unknown device tier."),
            },
            ImageId = device.ImageId,
            ManualState = device.ManualState == ManualDeviceState.Normal ? "NORMAL" : "TEMPORARILY_DISABLED",
            TemporaryUnavailableReason = device.TemporaryUnavailableReason?.Value,
            IsArchived = device.IsArchived,
            Version = version,
            CreatedAt = createdAtUtc.ToUniversalTime(),
            UpdatedAt = updatedAtUtc.ToUniversalTime(),
        };
    }

    public static Device ToDomain(DeviceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var tier = record.Tier switch
        {
            "LOW" => DeviceTier.Low,
            "MID" => DeviceTier.Mid,
            "HIGH" => DeviceTier.High,
            _ => throw new ArgumentOutOfRangeException(nameof(record), record.Tier, "Unknown device tier."),
        };
        var manualState = record.ManualState switch
        {
            "NORMAL" => ManualDeviceState.Normal,
            "TEMPORARILY_DISABLED" => ManualDeviceState.TemporarilyDisabled,
            _ => throw new ArgumentOutOfRangeException(nameof(record), record.ManualState, "Unknown manual device state."),
        };
        return new Device(
            record.Id,
            record.AssetNumber,
            record.ModelName,
            tier,
            record.ImageId,
            manualState,
            manualState == ManualDeviceState.TemporarilyDisabled
                ? Reason.From(record.TemporaryUnavailableReason ?? string.Empty)
                : null,
            record.IsArchived);
    }
}
