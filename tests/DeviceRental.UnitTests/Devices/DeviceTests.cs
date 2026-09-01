using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;
using Xunit;

namespace DeviceRental.UnitTests.Devices;

public sealed class DeviceTests
{
    private static readonly DateTimeOffset BorrowedAt = DateTimeOffset.Parse("2026-09-01T02:00:00Z");

    [Fact]
    public void Constructor_KeepsArchiveLifecycleSeparateFromManualAvailability()
    {
        var device = CreateDevice(isArchived: true);

        Assert.True(device.IsArchived);
        Assert.Equal(ManualDeviceState.Normal, device.ManualState);
        Assert.Equal(DeviceAvailability.Available, device.GetAvailability(null));
        Assert.False(device.IsBorrowable(null));
    }

    [Fact]
    public void Constructor_RequiresReasonExactlyWhenTemporarilyDisabled()
    {
        Assert.Throws<ArgumentException>(() => CreateDevice(ManualDeviceState.TemporarilyDisabled));
        Assert.Throws<ArgumentException>(() => CreateDevice(
            ManualDeviceState.Normal,
            Reason.From("not applicable")));

        var device = CreateDevice(
            ManualDeviceState.TemporarilyDisabled,
            Reason.From("screen damaged"));

        Assert.Equal(DeviceAvailability.Unavailable, device.GetAvailability(null));
        Assert.False(device.IsBorrowable(null));
    }

    [Fact]
    public void GetAvailability_OpenLoanAlwaysDerivesBorrowedEvenWhenOverdue()
    {
        var device = CreateDevice();
        var loan = Loan.Open(
            Guid.NewGuid(),
            device.Id,
            Guid.NewGuid(),
            BorrowedAt,
            BorrowedAt.AddHours(1),
            Guid.NewGuid());

        Assert.Equal(DeviceAvailability.Borrowed, device.GetAvailability(loan));
        Assert.Equal(LoanStatus.Overdue, loan.GetStatus(BorrowedAt.AddHours(2)));
        Assert.False(device.IsBorrowable(loan));
    }

    [Fact]
    public void GetAvailability_RejectsAClosedOrDifferentDeviceLoan()
    {
        var device = CreateDevice();
        var otherLoan = Loan.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            BorrowedAt,
            BorrowedAt.AddHours(1),
            Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => device.GetAvailability(otherLoan));
    }

    [Fact]
    public void Constructor_RejectsEmptyIdsTextAndUnknownEnums()
    {
        Assert.Throws<ArgumentException>(() => new Device(
            Guid.Empty,
            "asset",
            "model",
            DeviceTier.Low,
            Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new Device(
            Guid.NewGuid(),
            " ",
            "model",
            DeviceTier.Low,
            Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Device(
            Guid.NewGuid(),
            "asset",
            "model",
            (DeviceTier)99,
            Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new Device(
            Guid.NewGuid(),
            "asset",
            "model",
            DeviceTier.High,
            Guid.Empty));
    }

    [Fact]
    public void Enums_ExposeOnlyApprovedBusinessStates()
    {
        Assert.Equal(["Low", "Mid", "High"], Enum.GetNames<DeviceTier>());
        Assert.Equal(["Normal", "TemporarilyDisabled"], Enum.GetNames<ManualDeviceState>());
        Assert.Equal(["Available", "Borrowed", "Unavailable"], Enum.GetNames<DeviceAvailability>());
    }

    private static Device CreateDevice(
        ManualDeviceState state = ManualDeviceState.Normal,
        Reason? reason = null,
        bool isArchived = false) =>
        new(
            Guid.NewGuid(),
            " asset-001 ",
            " Test Phone ",
            DeviceTier.Mid,
            Guid.NewGuid(),
            state,
            reason,
            isArchived);
}
