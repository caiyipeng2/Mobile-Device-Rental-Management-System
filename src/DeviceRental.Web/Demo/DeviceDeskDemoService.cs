namespace DeviceRental.Web.Demo;

/// <summary>
/// A deliberately isolated, process-local data source for the clickable MVP.
/// It demonstrates the Web contract while the application and persistence layers
/// are being completed; it must be replaced by an application-layer adapter before release.
/// </summary>
public interface IDeviceDeskService
{
    DeviceDeskOverview GetOverview(DeviceDeskAvailability? availability, string? search = null);

    DeviceDeskOperationResult Borrow(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc);

    DeviceDeskOperationResult Return(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc);

    DeviceDeskOperationResult ForceReturn(
        string assetNumber,
        DemoCurrentUser user,
        DateTimeOffset nowUtc,
        string? reason);

    DeviceDeskOperationResult SetAvailability(
        string assetNumber,
        DeviceDeskAvailability availability,
        string? reason,
        DemoCurrentUser user);
}

public sealed class InMemoryDeviceDeskService : IDeviceDeskService
{
    private readonly object _gate = new();
    private readonly List<MutableDevice> _devices =
    [
        new("NAT-021", "iPhone 16 Pro", "高端", DeviceDeskAvailability.Available, "#7b5db2"),
        new("QA-014", "Galaxy S24 Ultra", "高端", DeviceDeskAvailability.Borrowed, "#7b5db2", "王蕾", DateTimeOffset.UtcNow.AddHours(20)),
        new("DEV-037", "Pixel 9", "中端", DeviceDeskAvailability.Borrowed, "#2e7b78", "周敏", DateTimeOffset.UtcNow.AddHours(-2)),
        new("LAB-052", "Redmi Note 14", "低端", DeviceDeskAvailability.Unavailable, "#697386", null, null, "摄像头维修中"),
        new("LAB-019", "iPhone 13 mini", "中端", DeviceDeskAvailability.Available, "#2e7b78"),
    ];

    public DeviceDeskOverview GetOverview(DeviceDeskAvailability? availability, string? search = null)
    {
        lock (_gate)
        {
            var normalizedSearch = search?.Trim();
            var devices = _devices
                .Where(device => availability is null || device.Availability == availability)
                .Where(device => string.IsNullOrWhiteSpace(normalizedSearch) ||
                    device.ModelName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    device.AssetNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .Select(device => device.ToSnapshot())
                .ToArray();

            return new DeviceDeskOverview(
                devices,
                new DeviceDeskSummary(
                    _devices.Count,
                    _devices.Count(device => device.Availability == DeviceDeskAvailability.Available),
                    _devices.Count(device => device.Availability == DeviceDeskAvailability.Borrowed),
                    _devices.Count(device => device.Availability == DeviceDeskAvailability.Unavailable)));
        }
    }

    public DeviceDeskOperationResult Borrow(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var device = Find(assetNumber);
            if (device is null)
            {
                return DeviceDeskOperationResult.Failure("未找到该设备，请刷新列表后重试。");
            }

            if (device.Availability != DeviceDeskAvailability.Available)
            {
                return DeviceDeskOperationResult.Failure("设备当前不可借用，列表已保留最新状态。");
            }

            device.Availability = DeviceDeskAvailability.Borrowed;
            device.BorrowerName = user.DisplayName;
            device.DueAtUtc = nowUtc.AddDays(1);
            device.UnavailableReason = null;
            return DeviceDeskOperationResult.Success($"已借用 {device.ModelName}，请在 24 小时内归还或联系管理员续借。");
        }
    }

    public DeviceDeskOperationResult Return(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            var device = Find(assetNumber);
            if (device is null || device.Availability != DeviceDeskAvailability.Borrowed)
            {
                return DeviceDeskOperationResult.Failure("该设备没有可归还的借用记录。");
            }

            if (!user.IsAdministrator && !string.Equals(device.BorrowerName, user.DisplayName, StringComparison.Ordinal))
            {
                return DeviceDeskOperationResult.Failure("只有借用人本人或测试组管理员可以归还该设备。");
            }

            device.Availability = DeviceDeskAvailability.Available;
            device.BorrowerName = null;
            device.DueAtUtc = null;
            return DeviceDeskOperationResult.Success($"已归还 {device.ModelName}，设备现在可借用。");
        }
    }

    public DeviceDeskOperationResult ForceReturn(
        string assetNumber,
        DemoCurrentUser user,
        DateTimeOffset nowUtc,
        string? reason)
    {
        if (!user.IsAdministrator)
        {
            return DeviceDeskOperationResult.Failure("只有测试组管理员可以强制归还设备。");
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return DeviceDeskOperationResult.Failure("强制归还需要填写原因。");
        }

        lock (_gate)
        {
            var device = Find(assetNumber);
            if (device is null || device.Availability != DeviceDeskAvailability.Borrowed)
            {
                return DeviceDeskOperationResult.Failure("该设备没有可归还的借用记录。");
            }

            device.Availability = DeviceDeskAvailability.Available;
            device.BorrowerName = null;
            device.DueAtUtc = null;
            return DeviceDeskOperationResult.Success($"已强制归还 {device.ModelName}：{normalizedReason}");
        }
    }

    public DeviceDeskOperationResult SetAvailability(
        string assetNumber,
        DeviceDeskAvailability availability,
        string? reason,
        DemoCurrentUser user)
    {
        if (!user.IsAdministrator)
        {
            return DeviceDeskOperationResult.Failure("只有测试组管理员可以变更设备可用状态。");
        }

        if (availability == DeviceDeskAvailability.Borrowed)
        {
            return DeviceDeskOperationResult.Failure("借用中状态只能通过借用操作产生。");
        }

        lock (_gate)
        {
            var device = Find(assetNumber);
            if (device is null)
            {
                return DeviceDeskOperationResult.Failure("未找到该设备，请刷新列表后重试。");
            }

            if (device.Availability == DeviceDeskAvailability.Borrowed)
            {
                return DeviceDeskOperationResult.Failure("请先归还正在借用的设备，再调整其可用状态。");
            }

            var unavailableReason = reason?.Trim();
            if (availability == DeviceDeskAvailability.Unavailable && string.IsNullOrWhiteSpace(unavailableReason))
            {
                return DeviceDeskOperationResult.Failure("暂停借用时必须填写原因。");
            }

            device.Availability = availability;
            device.UnavailableReason = availability == DeviceDeskAvailability.Unavailable ? unavailableReason : null;
            return availability == DeviceDeskAvailability.Unavailable
                ? DeviceDeskOperationResult.Success($"已暂停 {device.ModelName} 的借用：{device.UnavailableReason}")
                : DeviceDeskOperationResult.Success($"已恢复 {device.ModelName} 为可借用状态。");
        }
    }

    private MutableDevice? Find(string assetNumber) =>
        _devices.FirstOrDefault(device => string.Equals(device.AssetNumber, assetNumber, StringComparison.OrdinalIgnoreCase));

    private sealed class MutableDevice(
        string assetNumber,
        string modelName,
        string tier,
        DeviceDeskAvailability availability,
        string tierColor,
        string? borrowerName = null,
        DateTimeOffset? dueAtUtc = null,
        string? unavailableReason = null)
    {
        public string AssetNumber { get; } = assetNumber;

        public string ModelName { get; } = modelName;

        public string Tier { get; } = tier;

        public string TierColor { get; } = tierColor;

        public DeviceDeskAvailability Availability { get; set; } = availability;

        public string? BorrowerName { get; set; } = borrowerName;

        public DateTimeOffset? DueAtUtc { get; set; } = dueAtUtc;

        public string? UnavailableReason { get; set; } = unavailableReason;

        public DeviceDeskDevice ToSnapshot() => new(
            AssetNumber,
            ModelName,
            Tier,
            TierColor,
            Availability,
            BorrowerName,
            DueAtUtc,
            UnavailableReason);
    }
}

public sealed class DemoCurrentUserContext(IConfiguration configuration)
{
    public DemoCurrentUser GetCurrentUser()
    {
        var displayName = configuration["Demo:CurrentUserName"];
        var role = configuration["Demo:CurrentUserRole"];
        var isAdministrator = string.Equals(role, "TestAdmin", StringComparison.OrdinalIgnoreCase);

        return new DemoCurrentUser(
            string.IsNullOrWhiteSpace(displayName) ? "林乔" : displayName.Trim(),
            isAdministrator);
    }
}

public sealed record DemoCurrentUser(string DisplayName, bool IsAdministrator)
{
    public string RoleDisplayName => IsAdministrator ? "测试组管理员" : "普通用户";
}

public sealed record DeviceDeskOverview(IReadOnlyList<DeviceDeskDevice> Devices, DeviceDeskSummary Summary);

public sealed record DeviceDeskSummary(int Total, int Available, int Borrowed, int Unavailable);

public sealed record DeviceDeskDevice(
    string AssetNumber,
    string ModelName,
    string Tier,
    string TierColor,
    DeviceDeskAvailability Availability,
    string? BorrowerName,
    DateTimeOffset? DueAtUtc,
    string? UnavailableReason)
{
    public string StatusLabel => Availability switch
    {
        DeviceDeskAvailability.Available => "空闲",
        DeviceDeskAvailability.Borrowed => "借用中",
        DeviceDeskAvailability.Unavailable => "暂不可借",
        _ => throw new InvalidOperationException("Unsupported device availability."),
    };
}

public sealed record DeviceDeskOperationResult(bool Succeeded, string Message)
{
    public static DeviceDeskOperationResult Success(string message) => new(true, message);

    public static DeviceDeskOperationResult Failure(string message) => new(false, message);
}

public enum DeviceDeskAvailability
{
    Available,
    Borrowed,
    Unavailable,
}
