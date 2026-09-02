using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;

namespace DeviceRental.Application.Devices;

/// <summary>
/// Transactional persistence boundary for the device catalogue's state-changing loan operations.
/// Authorization and access-window decisions stay in the application service; this boundary keeps
/// the database-enforced open-loan invariant authoritative when requests race each other.
/// </summary>
public interface IDeviceCatalogStore
{
    Task<IReadOnlyList<DeviceCatalogStoreEntry>> ListUnarchivedAsync(
        CancellationToken cancellationToken = default);

    Task<DeviceCatalogStoreWriteResult> TryBorrowAsync(
        Loan loan,
        CancellationToken cancellationToken = default);

    Task<DeviceCatalogStoreWriteResult> ReturnSelfAsync(
        Guid deviceId,
        Guid borrowerId,
        DateTimeOffset returnedAtUtc,
        CancellationToken cancellationToken = default);

    Task<DeviceCatalogStoreWriteResult> ForceReturnAndDisableAsync(
        Guid deviceId,
        Guid administratorId,
        DateTimeOffset returnedAtUtc,
        Reason reason,
        CancellationToken cancellationToken = default);

    Task<DeviceCatalogStoreWriteResult> ExtendAsync(
        Guid deviceId,
        Guid administratorId,
        DurationMinutes duration,
        Reason reason,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record DeviceCatalogStoreEntry(
    Device Device,
    Loan? OpenLoan,
    string? BorrowerName);

public enum DeviceCatalogStoreWriteStatus
{
    Succeeded,
    DeviceNotFound,
    DeviceUnavailable,
    DeviceAlreadyBorrowed,
    NoActiveLoan,
    ReturnNotAuthorized,
}

public sealed record DeviceCatalogStoreWriteResult(
    DeviceCatalogStoreWriteStatus Status,
    Loan? Loan = null,
    LoanExtension? Extension = null)
{
    public static DeviceCatalogStoreWriteResult Success(Loan loan, LoanExtension? extension = null) =>
        new(DeviceCatalogStoreWriteStatus.Succeeded, loan, extension);

    public static DeviceCatalogStoreWriteResult Failure(DeviceCatalogStoreWriteStatus status) =>
        new(status);
}
