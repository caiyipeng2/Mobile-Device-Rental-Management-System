using DeviceRental.Application.Devices;
using DeviceRental.Application.Notifications;
using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using DeviceRental.Infrastructure.Persistence.Records;
using DeviceRental.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeviceRental.Infrastructure.Devices;

/// <summary>
/// PostgreSQL implementation of the device catalogue persistence boundary. The open-loan partial
/// unique index is intentionally the final arbiter for competing borrow requests, rather than an
/// application-side pre-check that would become stale before insertion.
/// </summary>
public sealed class EfDeviceCatalogStore(
    DeviceRentalDbContext dbContext,
    INotificationOutboxWriter? notificationOutboxWriter = null) : IDeviceCatalogStore
{
    private const string OpenLoanUniqueIndex = "ux_loans_open_device";
    private readonly LoanExtensionPolicy _extensionPolicy = new();
    private readonly LoanNotificationPlanner _notificationPlanner = new();

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
            var borrower = await dbContext.Users.AsNoTracking()
                .SingleAsync(user => user.Id == loan.BorrowerId, cancellationToken);
            var notificationValues = new Dictionary<string, string?>
            {
                ["deviceModel"] = deviceRecord.ModelName,
                ["assetNumber"] = deviceRecord.AssetNumber,
                ["borrowedAt"] = loan.BorrowedAtUtc.ToString("yyyy-MM-dd HH:mm"),
                ["dueAt"] = loan.DueAtUtc.ToString("yyyy-MM-dd HH:mm"),
            };
            EnqueueLoanNotification(
                loan,
                loanRecord.Version,
                "LOAN_BORROWED",
                borrower,
                loan.BorrowedAtUtc,
                notificationValues);
            EnqueueLoanReminders(loan, loanRecord.Version, borrower, loan.BorrowedAtUtc, notificationValues);
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
            await CancelPendingLoanRemindersAsync(loanRecord, returnedAtUtc, cancellationToken);
            var device = await dbContext.Devices.AsNoTracking()
                .SingleAsync(value => value.Id == deviceId, cancellationToken);
            var borrower = await dbContext.Users.AsNoTracking()
                .SingleAsync(user => user.Id == borrowerId, cancellationToken);
            EnqueueLoanNotification(
                returnedLoan,
                loanRecord.Version,
                "LOAN_RETURNED",
                borrower,
                returnedLoan.ReturnedAtUtc ?? returnedAtUtc,
                new Dictionary<string, string?>
                {
                    ["deviceModel"] = device.ModelName,
                    ["assetNumber"] = device.AssetNumber,
                    ["returnedAt"] = returnedLoan.ReturnedAtUtc?.ToString("yyyy-MM-dd HH:mm"),
                });
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
            await CancelPendingLoanRemindersAsync(loanRecord, returnedAtUtc, cancellationToken);
            deviceRecord.ManualState = "TEMPORARILY_DISABLED";
            deviceRecord.TemporaryUnavailableReason = reason.Value;
            deviceRecord.Version++;
            deviceRecord.UpdatedAt = returnedAtUtc.ToUniversalTime();
            var borrower = await dbContext.Users.AsNoTracking()
                .SingleAsync(user => user.Id == returnedLoan.BorrowerId, cancellationToken);
            EnqueueLoanNotification(
                returnedLoan,
                loanRecord.Version,
                "LOAN_FORCED_RETURN",
                borrower,
                returnedAtUtc,
                new Dictionary<string, string?>
                {
                    ["deviceModel"] = deviceRecord.ModelName,
                    ["assetNumber"] = deviceRecord.AssetNumber,
                    ["reason"] = reason.Value,
                });
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
            await CancelPendingLoanRemindersAsync(loanRecord, effectiveNowUtc, cancellationToken);
            var device = await dbContext.Devices.AsNoTracking()
                .SingleAsync(value => value.Id == deviceId, cancellationToken);
            var borrower = await dbContext.Users.AsNoTracking()
                .SingleAsync(user => user.Id == result.UpdatedLoan.BorrowerId, cancellationToken);
            EnqueueLoanNotification(
                result.UpdatedLoan,
                loanRecord.Version,
                "LOAN_EXTENDED",
                borrower,
                effectiveNowUtc,
                new Dictionary<string, string?>
                {
                    ["deviceModel"] = device.ModelName,
                    ["assetNumber"] = device.AssetNumber,
                    ["dueAt"] = result.UpdatedLoan.DueAtUtc.ToString("yyyy-MM-dd HH:mm"),
                });
            EnqueueLoanReminders(
                result.UpdatedLoan,
                loanRecord.Version,
                borrower,
                effectiveNowUtc,
                new Dictionary<string, string?>
                {
                    ["deviceModel"] = device.ModelName,
                    ["assetNumber"] = device.AssetNumber,
                    ["dueAt"] = result.UpdatedLoan.DueAtUtc.ToString("yyyy-MM-dd HH:mm"),
                });
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

    private void EnqueueLoanNotification(
        Loan loan,
        long aggregateVersion,
        string eventType,
        ApplicationUser borrower,
        DateTimeOffset createdAtUtc,
        IReadOnlyDictionary<string, string?> values,
        string? deduplicationKey = null,
        DateTimeOffset? availableAtUtc = null)
    {
        if (notificationOutboxWriter is null || string.IsNullOrWhiteSpace(borrower.Email))
        {
            return;
        }

        notificationOutboxWriter.Enqueue(new NotificationOutboxRequest(
            deduplicationKey ?? $"loan:{loan.Id:D}:{eventType.ToLowerInvariant()}",
            eventType,
            "LOAN",
            loan.Id.ToString("D"),
            aggregateVersion,
            $"loan:{loan.Id:D}:v{aggregateVersion}",
            new NotificationPayload(borrower.Email, borrower.RealName, values, borrower.Id),
            createdAtUtc,
            availableAtUtc));
    }

    private void EnqueueLoanReminders(
        Loan loan,
        long aggregateVersion,
        ApplicationUser borrower,
        DateTimeOffset scheduleCreatedAtUtc,
        IReadOnlyDictionary<string, string?> values)
    {
        if (notificationOutboxWriter is null || string.IsNullOrWhiteSpace(borrower.Email))
        {
            return;
        }

        var plan = _notificationPlanner.Create(
            loan.Id,
            aggregateVersion,
            scheduleCreatedAtUtc,
            loan.DueAtUtc);
        if (plan.AdvanceReminderAtUtc is { } advanceAtUtc)
        {
            EnqueueLoanNotification(
                loan,
                aggregateVersion,
                "LOAN_ADVANCE_REMINDER",
                borrower,
                scheduleCreatedAtUtc,
                values,
                plan.AdvanceReminderKey,
                advanceAtUtc);
        }

        EnqueueLoanNotification(
            loan,
            aggregateVersion,
            "LOAN_DUE",
            borrower,
            scheduleCreatedAtUtc,
            values,
            plan.DueReminderKey,
            plan.DueReminderAtUtc);
    }

    private Task<int> CancelPendingLoanRemindersAsync(
        LoanRecord loanRecord,
        DateTimeOffset canceledAtUtc,
        CancellationToken cancellationToken) =>
        notificationOutboxWriter is null
            ? Task.FromResult(0)
            : notificationOutboxWriter.CancelPendingRemindersAsync(
                "LOAN",
                loanRecord.Id.ToString("D"),
                canceledAtUtc,
                cancellationToken);
}
