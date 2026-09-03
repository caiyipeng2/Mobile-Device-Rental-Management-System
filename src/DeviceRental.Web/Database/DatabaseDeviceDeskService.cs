using DeviceRental.Application.Devices;
using DeviceRental.Application.Policy;
using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using DeviceRental.Infrastructure.Persistence.Records;
using DeviceRental.Web.Demo;
using Microsoft.EntityFrameworkCore;

namespace DeviceRental.Web.Database;

/// <summary>
/// Web-facing adapter for the production device desk. It keeps Razor DTOs out of Infrastructure
/// while routing state-changing operations through the transactional EF store. The synchronous
/// interface is retained by the MVP pages; each call still uses the scoped DbContext and store.
/// </summary>
public sealed class DatabaseDeviceDeskService(
    DeviceRentalDbContext dbContext,
    IDeviceCatalogStore catalogStore,
    IConfiguration configuration,
    ILoanPolicyStore policyStore) : IDeviceDeskService
{
    private static readonly Guid DefaultPolicyVersionId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");

    public int DefaultLoanMinutes
    {
        get
        {
            var persisted = policyStore.GetCurrentAsync(DateTimeOffset.UtcNow).GetAwaiter().GetResult();
            if (persisted is not null)
            {
                return persisted.Duration.Value;
            }

            return int.TryParse(configuration["Rental:DefaultLoanMinutes"], out var minutes) &&
                minutes is >= LoanExtensionPolicy.MinimumMinutes and <= LoanExtensionPolicy.MaximumMinutes
                    ? minutes
                    : 1_440;
        }
    }

    public DeviceDeskOverview GetOverview(DeviceDeskAvailability? availability, string? search = null)
    {
        var entries = catalogStore.ListUnarchivedAsync().GetAwaiter().GetResult();
        var normalizedSearch = search?.Trim();
        var devices = entries
            .Select(ToDevice)
            .Where(device => availability is null || device.Availability == availability)
            .Where(device => string.IsNullOrWhiteSpace(normalizedSearch) ||
                device.ModelName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                device.AssetNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new DeviceDeskOverview(
            devices,
            new DeviceDeskSummary(
                entries.Count,
                entries.Count(entry => entry.Device.GetAvailability(entry.OpenLoan) == DeviceAvailability.Available),
                entries.Count(entry => entry.Device.GetAvailability(entry.OpenLoan) == DeviceAvailability.Borrowed),
                entries.Count(entry => entry.Device.GetAvailability(entry.OpenLoan) == DeviceAvailability.Unavailable)));
    }

    public IReadOnlyList<DeviceDeskLoan> GetLoans(DemoCurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.UserId is null)
        {
            return [];
        }

        var query =
            from loan in dbContext.Loans.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking() on loan.DeviceId equals device.Id
            join borrower in dbContext.Users.AsNoTracking() on loan.BorrowerId equals borrower.Id
            where user.IsAdministrator || loan.BorrowerId == user.UserId.Value
            orderby loan.BorrowedAt descending
            select new { Loan = loan, Device = device, Borrower = borrower };

        return query
            .ToList()
            .Select(row =>
            {
                var loan = LoanRecordMapper.ToDomain(row.Loan);
                return new DeviceDeskLoan(
                    row.Device.AssetNumber,
                    row.Device.ModelName,
                    row.Borrower.RealName,
                    loan.BorrowedAtUtc,
                    loan.DueAtUtc,
                    loan.ReturnedAtUtc,
                    loan.ReturnReason?.Value);
            })
            .ToArray();
    }

    public DeviceDeskOperationResult Borrow(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc)
    {
        if (user.UserId is null)
        {
            return DeviceDeskOperationResult.Failure("登录账户缺少有效身份标识，请重新登录。");
        }

        var entry = Find(assetNumber);
        if (entry is null)
        {
            return DeviceDeskOperationResult.Failure("未找到该设备，请刷新列表后重试。");
        }

        var borrowedAt = nowUtc.ToUniversalTime();
        var policy = policyStore.GetCurrentAsync(borrowedAt).GetAwaiter().GetResult();
        var loanMinutes = policy?.Duration.Value ?? DefaultLoanMinutes;
        var policyVersionId = policy?.Id ?? DefaultPolicyVersionId;
        var loan = Loan.Open(
            Guid.NewGuid(),
            entry.Device.Id,
            user.UserId.Value,
            borrowedAt,
            borrowedAt.AddMinutes(loanMinutes),
            policyVersionId);
        var result = catalogStore.TryBorrowAsync(loan).GetAwaiter().GetResult();
        return result.Status switch
        {
            DeviceCatalogStoreWriteStatus.Succeeded =>
                DeviceDeskOperationResult.Success($"已借用 {entry.Device.ModelName}，请在 {loanMinutes} 分钟内归还或联系管理员续借。"),
            DeviceCatalogStoreWriteStatus.DeviceAlreadyBorrowed =>
                DeviceDeskOperationResult.Failure("设备刚刚被其他人借用，请刷新列表后重试。"),
            DeviceCatalogStoreWriteStatus.DeviceUnavailable =>
                DeviceDeskOperationResult.Failure("设备当前暂不可借用。"),
            DeviceCatalogStoreWriteStatus.DeviceNotFound =>
                DeviceDeskOperationResult.Failure("未找到该设备，请刷新列表后重试。"),
            _ => DeviceDeskOperationResult.Failure("借用未完成，请稍后重试。"),
        };
    }

    public DeviceDeskOperationResult Return(string assetNumber, DemoCurrentUser user, DateTimeOffset nowUtc)
    {
        if (user.UserId is null)
        {
            return DeviceDeskOperationResult.Failure("登录账户缺少有效身份标识，请重新登录。");
        }

        var entry = Find(assetNumber);
        if (entry?.OpenLoan is null)
        {
            return DeviceDeskOperationResult.Failure("该设备没有可归还的借用记录。");
        }

        var result = catalogStore.ReturnSelfAsync(
                entry.Device.Id,
                user.UserId.Value,
                nowUtc.ToUniversalTime())
            .GetAwaiter()
            .GetResult();
        return result.Status switch
        {
            DeviceCatalogStoreWriteStatus.Succeeded => DeviceDeskOperationResult.Success($"已归还 {entry.Device.ModelName}，设备现在可借用。"),
            DeviceCatalogStoreWriteStatus.ReturnNotAuthorized => DeviceDeskOperationResult.Failure("只有借用人本人可以执行本人归还。"),
            DeviceCatalogStoreWriteStatus.NoActiveLoan => DeviceDeskOperationResult.Failure("该设备没有可归还的借用记录。"),
            _ => DeviceDeskOperationResult.Failure("归还未完成，请刷新列表后重试。"),
        };
    }

    public DeviceDeskOperationResult ForceReturn(
        string assetNumber,
        DemoCurrentUser user,
        DateTimeOffset nowUtc,
        string? reason)
    {
        if (!user.IsAdministrator || user.UserId is null)
        {
            return DeviceDeskOperationResult.Failure("只有测试组管理员可以强制归还设备。");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return DeviceDeskOperationResult.Failure("强制归还需要填写原因。");
        }

        var entry = Find(assetNumber);
        if (entry is null)
        {
            return DeviceDeskOperationResult.Failure("未找到该设备，请刷新列表后重试。");
        }

        var result = catalogStore.ForceReturnAndDisableAsync(
                entry.Device.Id,
                user.UserId.Value,
                nowUtc.ToUniversalTime(),
                Reason.From(reason))
            .GetAwaiter()
            .GetResult();
        return result.Status == DeviceCatalogStoreWriteStatus.Succeeded
            ? DeviceDeskOperationResult.Success($"已强制归还并暂停 {entry.Device.ModelName}：{reason.Trim()}")
            : DeviceDeskOperationResult.Failure("强制归还未完成，请刷新列表后重试。");
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

        var entry = Find(assetNumber);
        if (entry is null)
        {
            return DeviceDeskOperationResult.Failure("未找到该设备，请刷新列表后重试。");
        }

        if (entry.OpenLoan is not null)
        {
            return DeviceDeskOperationResult.Failure("请先归还正在借用的设备，再调整其可用状态。");
        }

        var unavailableReason = reason?.Trim();
        if (availability == DeviceDeskAvailability.Unavailable && string.IsNullOrWhiteSpace(unavailableReason))
        {
            return DeviceDeskOperationResult.Failure("暂停借用时必须填写原因。");
        }

        var record = dbContext.Devices.SingleOrDefault(device => device.Id == entry.Device.Id);
        if (record is null)
        {
            return DeviceDeskOperationResult.Failure("未找到该设备，请刷新列表后重试。");
        }

        record.ManualState = availability == DeviceDeskAvailability.Unavailable
            ? "TEMPORARILY_DISABLED"
            : "NORMAL";
        record.TemporaryUnavailableReason = availability == DeviceDeskAvailability.Unavailable
            ? unavailableReason
            : null;
        record.Version++;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.SaveChanges();
        return availability == DeviceDeskAvailability.Unavailable
            ? DeviceDeskOperationResult.Success($"已暂停 {entry.Device.ModelName} 的借用：{unavailableReason}")
            : DeviceDeskOperationResult.Success($"已恢复 {entry.Device.ModelName} 为可借用状态。");
    }

    public DeviceDeskOperationResult AddDevice(
        string assetNumber,
        string modelName,
        string tier,
        string? imageReference,
        DemoCurrentUser user)
    {
        // The old demo form accepts a display reference. Production requires a validated upload
        // and metadata row, so refuse the ambiguous reference instead of creating an orphaned device.
        return DeviceDeskOperationResult.Failure(
            "生产环境新增设备必须先完成图片上传校验并生成图片元数据，请使用正式上传入口。");
    }

    public DeviceDeskOperationResult SetDefaultLoanMinutes(
        int minutes,
        string? reason,
        DemoCurrentUser user)
    {
        if (!user.IsAdministrator || user.UserId is null)
        {
            return DeviceDeskOperationResult.Failure("只有测试组管理员可以修改默认借期。");
        }

        if (minutes is < LoanExtensionPolicy.MinimumMinutes or > LoanExtensionPolicy.MaximumMinutes)
        {
            return DeviceDeskOperationResult.Failure(
                $"借期必须在 {LoanExtensionPolicy.MinimumMinutes} 到 {LoanExtensionPolicy.MaximumMinutes} 分钟之间。");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return DeviceDeskOperationResult.Failure("修改借期时必须填写原因。");
        }

        policyStore.CreateAsync(
                DurationMinutes.From(minutes),
                user.UserId.Value,
                Reason.From(reason),
                DateTimeOffset.UtcNow)
            .GetAwaiter()
            .GetResult();
        return DeviceDeskOperationResult.Success($"默认借期已更新为 {minutes} 分钟。");
    }

    private DeviceCatalogStoreEntry? Find(string assetNumber)
    {
        var normalized = assetNumber?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : catalogStore.ListUnarchivedAsync()
                .GetAwaiter()
                .GetResult()
                .SingleOrDefault(entry => string.Equals(
                    entry.Device.AssetNumber,
                    normalized,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static DeviceDeskDevice ToDevice(DeviceCatalogStoreEntry entry)
    {
        var availability = entry.Device.GetAvailability(entry.OpenLoan);
        var tier = entry.Device.Tier switch
        {
            DeviceTier.Low => "低端",
            DeviceTier.Mid => "中端",
            DeviceTier.High => "高端",
            _ => throw new ArgumentOutOfRangeException(),
        };
        var tierColor = entry.Device.Tier switch
        {
            DeviceTier.Low => "#697386",
            DeviceTier.Mid => "#2e7b78",
            _ => "#2459d3",
        };
        return new DeviceDeskDevice(
            entry.Device.AssetNumber,
            entry.Device.ModelName,
            tier,
            tierColor,
            availability switch
            {
                DeviceAvailability.Available => DeviceDeskAvailability.Available,
                DeviceAvailability.Borrowed => DeviceDeskAvailability.Borrowed,
                DeviceAvailability.Unavailable => DeviceDeskAvailability.Unavailable,
                _ => throw new ArgumentOutOfRangeException(),
            },
            entry.BorrowerName,
            entry.OpenLoan?.DueAtUtc,
            entry.Device.TemporaryUnavailableReason?.Value,
            $"/devices/{entry.Device.Id:D}/image");
    }
}
