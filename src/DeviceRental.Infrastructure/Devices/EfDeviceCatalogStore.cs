using DeviceRental.Application.Devices;
using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeviceRental.Infrastructure.Devices;

/// <summary>
/// PostgreSQL implementation of the device catalogue persistence boundary. The open-loan partial
/// unique index is intentionally the final arbiter for competing borrow requests, rather than an
/// application-side pre-check that would become stale before insertion.
/// </summary>
public sealed class EfDeviceCatalogStore(DeviceRentalDbContext dbContext) : IDeviceCatalogStore
{
    private const string OpenLoanUniqueIndex = "ux_loans_open_device";
    private readonly LoanExtensionPolicy _extensionPolicy = new();

    public async Task<IReadOnlyList<DeviceCatalogStoreEntry>> ListUnarchivedAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var records = await (
            from device in dbContext.Devices.AsNoTracking()
            where !device.IsArchived
            join loan in dbContext.Loans.AsNoTracking().Where(value => value.ReturnedAt == null)
                on device.Id equals loan.DeviceId into openLoans
            from loan in openLoans.DefaultIfEmpty()
            join borrower in dbContext.Users.AsNoTracking()
                on loan.BorrowerId equals borrower.Id into borrowers
            from borrower in borrowers.DefaultIfEmpty()
            orderby device.AssetNumber
            select new { Device = device, Loan = loan, BorrowerName = borrower == null ? null : borrower.RealName })
            .ToListAsync(cancellationToken);

        return records.Select(record => new DeviceCatalogStoreEntry(
            DeviceRecordMapper.ToDomain(record.Device),
            record.Loan is null ? null : LoanRecordMapper.ToDomain(record.Loan),
            record.BorrowerName)).ToArray();
    }

    public async Task<DeviceCatalogStoreWriteResult> TryBorrowAsync(
        Loan loan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deviceRecord = await dbContext.Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(device => device.Id == loan.DeviceId, cancellationToken);
            if (deviceRecord is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.DeviceNotFound);
            }

            if (!DeviceRecordMapper.ToDomain(deviceRecord).IsBorrowable(null))
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.DeviceUnavailable);
            }

            var loanRecord = LoanRecordMapper.ToRecord(loan);
            dbContext.Loans.Add(loanRecord);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsOpenLoanUniqueConflict(exception))
            {
                // The failed entity must not remain tracked if this scoped context serves another request.
                dbContext.Entry(loanRecord).State = EntityState.Detached;
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.DeviceAlreadyBorrowed);
            }

            await transaction.CommitAsync(cancellationToken);
            return DeviceCatalogStoreWriteResult.Success(loan);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<DeviceCatalogStoreWriteResult> ReturnSelfAsync(
        Guid deviceId,
        Guid borrowerId,
        DateTimeOffset returnedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var loanRecord = await GetOpenLoanForUpdateAsync(deviceId, cancellationToken);
            if (loanRecord is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.NoActiveLoan);
            }

            if (loanRecord.BorrowerId != borrowerId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.ReturnNotAuthorized);
            }

            var returnedLoan = LoanRecordMapper.ToDomain(loanRecord).Close(
                returnedAtUtc,
                borrowerId,
                ReturnKind.Self,
                returnReason: null);
            ApplyLoan(loanRecord, returnedLoan);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DeviceCatalogStoreWriteResult.Success(returnedLoan);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<DeviceCatalogStoreWriteResult> ForceReturnAndDisableAsync(
        Guid deviceId,
        Guid administratorId,
        DateTimeOffset returnedAtUtc,
        Reason reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var deviceRecord = await GetDeviceForUpdateAsync(deviceId, cancellationToken);
            if (deviceRecord is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.DeviceNotFound);
            }

            var loanRecord = await GetOpenLoanForUpdateAsync(deviceId, cancellationToken);
            if (loanRecord is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.NoActiveLoan);
            }

            var returnedLoan = LoanRecordMapper.ToDomain(loanRecord).Close(
                returnedAtUtc,
                administratorId,
                ReturnKind.Forced,
                reason);
            ApplyLoan(loanRecord, returnedLoan);
            deviceRecord.ManualState = "TEMPORARILY_DISABLED";
            deviceRecord.TemporaryUnavailableReason = reason.Value;
            deviceRecord.Version++;
            deviceRecord.UpdatedAt = returnedAtUtc.ToUniversalTime();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DeviceCatalogStoreWriteResult.Success(returnedLoan);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<DeviceCatalogStoreWriteResult> ExtendAsync(
        Guid deviceId,
        Guid administratorId,
        DurationMinutes duration,
        Reason reason,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(duration);
        ArgumentNullException.ThrowIfNull(reason);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var loanRecord = await GetOpenLoanForUpdateAsync(deviceId, cancellationToken);
            if (loanRecord is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeviceCatalogStoreWriteResult.Failure(DeviceCatalogStoreWriteStatus.NoActiveLoan);
            }

            var result = _extensionPolicy.Create(
                Guid.NewGuid(),
                LoanRecordMapper.ToDomain(loanRecord),
                administratorId,
                duration,
                reason,
                effectiveNowUtc);
            ApplyLoan(loanRecord, result.UpdatedLoan);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DeviceCatalogStoreWriteResult.Success(result.UpdatedLoan, result.Extension);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Task<LoanRecord?> GetOpenLoanForUpdateAsync(Guid deviceId, CancellationToken cancellationToken) =>
        dbContext.Loans
            .FromSqlInterpolated($"SELECT * FROM device_rental.loans WHERE device_id = {deviceId} AND returned_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private Task<DeviceRecord?> GetDeviceForUpdateAsync(Guid deviceId, CancellationToken cancellationToken) =>
        dbContext.Devices
            .FromSqlInterpolated($"SELECT * FROM device_rental.devices WHERE id = {deviceId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private static bool IsOpenLoanUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: OpenLoanUniqueIndex,
        };

    private static void ApplyLoan(LoanRecord record, Loan loan)
    {
        record.DueAt = loan.DueAtUtc;
        record.ReturnedAt = loan.ReturnedAtUtc;
        record.ReturnedByUserId = loan.ReturnedByUserId;
        record.ReturnKind = loan.ReturnKind switch
        {
            null => null,
            ReturnKind.Self => "SELF",
            ReturnKind.Forced => "FORCED",
            _ => throw new ArgumentOutOfRangeException(nameof(loan), loan.ReturnKind, "Unknown return kind."),
        };
        record.ReturnReason = loan.ReturnReason?.Value;
        record.Version++;
    }
}
