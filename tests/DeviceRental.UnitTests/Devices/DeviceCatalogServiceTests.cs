using DeviceRental.Application.Devices;
using DeviceRental.Application.Policy;
using DeviceRental.Domain.Devices;
using Xunit;

namespace DeviceRental.UnitTests.Devices;

public sealed class DeviceCatalogServiceTests
{
    private static readonly Guid AdminId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset OpenTime = DateTimeOffset.Parse("2026-09-01T02:00:00Z");

    [Fact]
    [Trait("Requirement", "REQ-DEV-003")]
    public void Register_requires_admin_and_starts_available()
    {
        var catalog = CreateCatalog();
        var command = new RegisterDeviceCommand(
            "QA-IPHONE-001", "iPhone 16 Pro", DeviceTier.High, Guid.NewGuid());

        Assert.Throws<UnauthorizedAccessException>(() =>
            catalog.Register(command, UserId, isAdministrator: false, OpenTime));

        var device = catalog.Register(command, AdminId, isAdministrator: true, OpenTime);

        Assert.Equal(DeviceAvailability.Available, device.Availability);
        Assert.Equal("iPhone 16 Pro", device.ModelName);
    }

    [Fact]
    [Trait("Requirement", "REQ-LOAN-004")]
    [Trait("Requirement", "REQ-LOAN-005")]
    public void Concurrent_borrow_attempts_have_one_winner()
    {
        var catalog = CreateCatalog();
        var device = Register(catalog, "QA-PIXEL-001");
        var results = Enumerable.Range(0, 2)
            .AsParallel()
            .Select(index => catalog.Borrow(
                device.DeviceId,
                index == 0 ? UserId : OtherUserId,
                index == 0 ? "测试员甲" : "测试员乙",
                OpenTime))
            .ToArray();

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == DeviceCatalogError.DeviceAlreadyBorrowed);
        Assert.Equal(DeviceAvailability.Borrowed, catalog.Get(device.DeviceId)!.Availability);
    }

    [Fact]
    [Trait("Requirement", "REQ-LOAN-007")]
    public void Only_current_borrower_can_self_return()
    {
        var catalog = CreateCatalog();
        var device = Register(catalog, "QA-IPHONE-002");
        Assert.True(catalog.Borrow(device.DeviceId, UserId, "测试员甲", OpenTime).Succeeded);

        var denied = catalog.Return(device.DeviceId, OtherUserId, isAdministrator: false, OpenTime, reason: null);
        Assert.False(denied.Succeeded);
        Assert.Equal(DeviceCatalogError.ReturnNotAuthorized, denied.ErrorCode);

        var returned = catalog.Return(device.DeviceId, UserId, isAdministrator: false, OpenTime, reason: null);
        Assert.True(returned.Succeeded);
        Assert.Equal(DeviceAvailability.Available, catalog.Get(device.DeviceId)!.Availability);
    }

    [Fact]
    [Trait("Requirement", "REQ-DEV-009")]
    public void Administrator_can_force_return_and_disable_atomically()
    {
        var catalog = CreateCatalog();
        var device = Register(catalog, "QA-SAMSUNG-001");
        Assert.True(catalog.Borrow(device.DeviceId, UserId, "测试员甲", OpenTime).Succeeded);

        var result = catalog.ForceReturnAndDisable(
            device.DeviceId,
            AdminId,
            isAdministrator: true,
            OpenTime,
            "屏幕维修");

        Assert.True(result.Succeeded);
        var current = catalog.Get(device.DeviceId)!;
        Assert.Equal(DeviceAvailability.Unavailable, current.Availability);
        Assert.Equal("屏幕维修", current.UnavailableReason);
        Assert.Null(current.BorrowerId);
    }

    [Fact]
    [Trait("Requirement", "REQ-LOAN-012")]
    public void Administrator_extension_updates_due_time_without_mutating_history_value()
    {
        var catalog = CreateCatalog();
        var device = Register(catalog, "QA-XIAOMI-001");
        var borrowed = catalog.Borrow(device.DeviceId, UserId, "测试员甲", OpenTime);
        var before = borrowed.Value!.DueAtUtc;

        var extension = catalog.Extend(
            device.DeviceId,
            AdminId,
            isAdministrator: true,
            DeviceRental.Domain.Common.DurationMinutes.From(60),
            "回归测试仍未结束",
            OpenTime.AddHours(1));

        Assert.True(extension.Succeeded);
        Assert.True(extension.Value!.DueAtUtc > before);
        Assert.Equal(before, extension.Value.PreviousDueAtUtc);
    }

    [Fact]
    [Trait("Requirement", "REQ-TIME-003")]
    public void Closed_window_rejects_borrow_and_returns_next_open_time()
    {
        var catalog = CreateCatalog();
        var device = Register(catalog, "QA-HUAWEI-001");

        var result = catalog.Borrow(device.DeviceId, UserId, "测试员甲", OpenTime.AddHours(9));

        Assert.False(result.Succeeded);
        Assert.Equal(DeviceCatalogError.OutsideAccessWindow, result.ErrorCode);
        Assert.Equal(DateTimeOffset.Parse("2026-09-02T01:00:00Z"), result.NextOpenUtc);
    }

    private static DeviceCatalogService CreateCatalog() =>
        new(new AccessWindowPolicy());

    private static DeviceSummary Register(DeviceCatalogService catalog, string assetNumber) =>
        catalog.Register(
            new RegisterDeviceCommand(assetNumber, assetNumber, DeviceTier.Mid, Guid.NewGuid()),
            AdminId,
            isAdministrator: true,
            OpenTime);
}
