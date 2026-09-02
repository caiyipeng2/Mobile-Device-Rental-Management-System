using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Persistence.Mappers;
using Xunit;

namespace DeviceRental.IntegrationTests.Persistence;

public sealed class DeviceLoanMapperTests
{
    private static readonly DateTimeOffset BorrowedAt = DateTimeOffset.Parse("2026-09-01T02:00:00Z");

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-DEV-003")]
    public void DeviceRecord_RoundTripsTierAndTemporaryUnavailableReason()
    {
        var device = new Device(
            Guid.NewGuid(),
            "QA-001",
            "iPhone 16 Pro",
            DeviceTier.High,
            Guid.NewGuid(),
            ManualDeviceState.TemporarilyDisabled,
            Reason.From("屏幕维修"));

        var record = DeviceRecordMapper.ToRecord(device, BorrowedAt, BorrowedAt.AddMinutes(1));
        var roundTripped = DeviceRecordMapper.ToDomain(record);

        Assert.Equal(device.Id, roundTripped.Id);
        Assert.Equal(device.AssetNumber, roundTripped.AssetNumber);
        Assert.Equal(device.Tier, roundTripped.Tier);
        Assert.Equal(device.ManualState, roundTripped.ManualState);
        Assert.Equal("屏幕维修", roundTripped.TemporaryUnavailableReason!.Value);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-LOAN-009")]
    public void LoanRecord_RoundTripsOpenAndForcedReturnTuples()
    {
        var open = Loan.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            BorrowedAt,
            BorrowedAt.AddDays(1),
            Guid.NewGuid());
        var openRecord = LoanRecordMapper.ToRecord(open);
        var openRoundTrip = LoanRecordMapper.ToDomain(openRecord);
        Assert.True(openRoundTrip.IsOpen);

        var returned = open.Close(
            BorrowedAt.AddHours(2),
            Guid.NewGuid(),
            ReturnKind.Forced,
            Reason.From("设备维修"));
        var returnedRoundTrip = LoanRecordMapper.ToDomain(LoanRecordMapper.ToRecord(returned));

        Assert.False(returnedRoundTrip.IsOpen);
        Assert.Equal(ReturnKind.Forced, returnedRoundTrip.ReturnKind);
        Assert.Equal("设备维修", returnedRoundTrip.ReturnReason!.Value);
    }
}
