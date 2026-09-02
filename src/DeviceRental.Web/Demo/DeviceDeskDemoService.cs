namespace DeviceRental.Web.Demo;

/// <summary>
/// A deliberately isolated, process-local data source for the clickable MVP.
/// It demonstrates the Web contract while the application and persistence layers
/// are being completed; it must be replaced by an application-layer adapter before release.
/// </summary>
public interface IDeviceDeskService
{
    DeviceDeskOverview GetOverview(DeviceDeskAvailability? availability, string? search = null);

    int DefaultLoanMinutes { get; }

    IReadOnlyList<DeviceDeskLoan> GetLoans(DemoCurrentUser user);

    DeviceDeskOperationResult Borrow(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc);

    DeviceDeskOperationResult Return(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc);

    DeviceDeskOperationResult ForceReturn(
        string assetNumber,
        DemoCurrentUser user,
        DateTimeOffset nowUtc,
        string? reason);

    DeviceDeskOperationResult AddDevice(
        string assetNumber,
        string modelName,
        string tier,
        string? imageReference,
        DemoCurrentUser user);

    DeviceDeskOperationResult SetDefaultLoanMinutes(
        int minutes,
        string? reason,
        DemoCurrentUser user);

    DeviceDeskOperationResult SetAvailability(
        string assetNumber,
        DeviceDeskAvailability availability,
        string? reason,
        DemoCurrentUser user);
}

public sealed class InMemoryDeviceDeskService : IDeviceDeskService
{
    private readonly object _gate = new();
    private readonly List<MutableLoan> _loans = [];
    private int _defaultLoanMinutes = 1_440;

    public int DefaultLoanMinutes
    {
        get
        {
            lock (_gate)
            {
                return _defaultLoanMinutes;
            }
        }
    }
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
            device.DueAtUtc = nowUtc.AddMinutes(_defaultLoanMinutes);
            device.UnavailableReason = null;
            _loans.Add(new MutableLoan(
                device.AssetNumber,
                device.ModelName,
                user.DisplayName,
                nowUtc,
                device.DueAtUtc.Value));
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
            CloseLoan(device, user.DisplayName, nowUtc, null);
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
            CloseLoan(device, user.DisplayName, nowUtc, normalizedReason);
            return DeviceDeskOperationResult.Success($"已强制归还 {device.ModelName}：{normalizedReason}");
        }
    }

    public DeviceDeskOperationResult AddDevice(
        string assetNumber,
        string modelName,
        string tier,
        string? imageReference,
        DemoCurrentUser user)
    {
        if (!user.IsAdministrator)
        {
            return DeviceDeskOperationResult.Failure("只有测试组管理员可以新增设备。");
        }

        var normalizedAsset = assetNumber?.Trim();
        var normalizedModel = modelName?.Trim();
        var normalizedTier = tier?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAsset) || string.IsNullOrWhiteSpace(normalizedModel))
        {
            return DeviceDeskOperationResult.Failure("资产编号和型号名称均为必填项。");
        }

        var normalizedImageReference = imageReference?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedImageReference))
        {
            return DeviceDeskOperationResult.Failure("新增设备时必须提供展示图引用。");
        }

        if (normalizedTier is not ("低端" or "中端" or "高端"))
        {
            return DeviceDeskOperationResult.Failure("档位必须选择低端、中端或高端。");
        }

        lock (_gate)
        {
            if (Find(normalizedAsset) is not null)
            {
                return DeviceDeskOperationResult.Failure("资产编号已存在，请换一个编号。");
            }

            var tierColor = normalizedTier switch
            {
                "低端" => "#697386",
                "中端" => "#2e7b78",
                _ => "#7b5db2",
            };
            _devices.Add(new MutableDevice(
                normalizedAsset,
                normalizedModel,
                normalizedTier,
                DeviceDeskAvailability.Available,
                tierColor,
                imageReference: normalizedImageReference));
            return DeviceDeskOperationResult.Success($"已新增设备 {normalizedModel}（{normalizedAsset}）。");
        }
    }

    public DeviceDeskOperationResult SetDefaultLoanMinutes(
        int minutes,
        string? reason,
        DemoCurrentUser user)
    {
        if (!user.IsAdministrator)
        {
            return DeviceDeskOperationResult.Failure("只有测试组管理员可以修改默认借期。");
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return DeviceDeskOperationResult.Failure("修改借期时必须填写原因。");
        }

        if (minutes is < 60 or > 10_080)
        {
            return DeviceDeskOperationResult.Failure("默认借期必须在 60 分钟至 7 天之间。");
        }

        lock (_gate)
        {
            _defaultLoanMinutes = minutes;
            return DeviceDeskOperationResult.Success($"默认借期已更新为 {minutes} 分钟：{normalizedReason}");
        }
    }

    public IReadOnlyList<DeviceDeskLoan> GetLoans(DemoCurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        lock (_gate)
        {
            var snapshots = _loans
                .Where(loan => user.IsAdministrator ||
                    string.Equals(loan.BorrowerName, user.DisplayName, StringComparison.Ordinal))
                .Select(loan => loan.ToSnapshot())
                .ToList();

            if (user.IsAdministrator)
            {
                foreach (var device in _devices.Where(device =>
                    device.Availability == DeviceDeskAvailability.Borrowed &&
                    _loans.All(loan => !string.Equals(
                        loan.AssetNumber,
                        device.AssetNumber,
                        StringComparison.OrdinalIgnoreCase))))
                {
                    snapshots.Add(new DeviceDeskLoan(
                        device.AssetNumber,
                        device.ModelName,
                        device.BorrowerName ?? "未知用户",
                        device.DueAtUtc?.AddDays(-1) ?? DateTimeOffset.UtcNow,
                        device.DueAtUtc,
                        null,
                        null));
                }
            }

            return snapshots
                .OrderByDescending(loan => loan.BorrowedAtUtc)
                .ToArray();
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

    private void CloseLoan(
        MutableDevice device,
        string returnedBy,
        DateTimeOffset returnedAtUtc,
        string? reason)
    {
        var loan = _loans.LastOrDefault(item =>
            string.Equals(item.AssetNumber, device.AssetNumber, StringComparison.OrdinalIgnoreCase) &&
            item.ReturnedAtUtc is null);
        if (loan is not null)
        {
            loan.ReturnedAtUtc = returnedAtUtc;
            loan.ReturnedByName = returnedBy;
            loan.ReturnReason = reason;
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
        string? unavailableReason = null,
        string? imageReference = null)
    {
        public string AssetNumber { get; } = assetNumber;

        public string ModelName { get; } = modelName;

        public string Tier { get; } = tier;

        public string TierColor { get; } = tierColor;

        public DeviceDeskAvailability Availability { get; set; } = availability;

        public string? BorrowerName { get; set; } = borrowerName;

        public DateTimeOffset? DueAtUtc { get; set; } = dueAtUtc;

        public string? UnavailableReason { get; set; } = unavailableReason;

        public string? ImageReference { get; } = imageReference;

        public DeviceDeskDevice ToSnapshot() => new(
            AssetNumber,
            ModelName,
            Tier,
            TierColor,
            Availability,
            BorrowerName,
            DueAtUtc,
            UnavailableReason,
            ImageReference);
    }

    private sealed class MutableLoan(
        string assetNumber,
        string modelName,
        string borrowerName,
        DateTimeOffset borrowedAtUtc,
        DateTimeOffset dueAtUtc)
    {
        public string AssetNumber { get; } = assetNumber;

        public string ModelName { get; } = modelName;

        public string BorrowerName { get; } = borrowerName;

        public DateTimeOffset BorrowedAtUtc { get; } = borrowedAtUtc;

        public DateTimeOffset DueAtUtc { get; } = dueAtUtc;

        public DateTimeOffset? ReturnedAtUtc { get; set; }

        public string? ReturnedByName { get; set; }

        public string? ReturnReason { get; set; }

        public DeviceDeskLoan ToSnapshot() => new(
            AssetNumber,
            ModelName,
            BorrowerName,
            BorrowedAtUtc,
            DueAtUtc,
            ReturnedAtUtc,
            ReturnReason);
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

public sealed record DeviceDeskLoan(
    string AssetNumber,
    string ModelName,
    string BorrowerName,
    DateTimeOffset BorrowedAtUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? ReturnedAtUtc,
    string? ReturnReason)
{
    public bool IsOpen => ReturnedAtUtc is null;

    public string StatusLabel => IsOpen ? "进行中" : "已归还";
}

public sealed record DeviceDeskSummary(int Total, int Available, int Borrowed, int Unavailable);

public sealed record DeviceDeskDevice(
    string AssetNumber,
    string ModelName,
    string Tier,
    string TierColor,
    DeviceDeskAvailability Availability,
    string? BorrowerName,
    DateTimeOffset? DueAtUtc,
    string? UnavailableReason,
    string? ImageReference = null)
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
