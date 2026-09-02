using DeviceRental.Application.Policy;
using DeviceRental.Domain.Common;
using DeviceRental.Domain.Devices;
using DeviceRental.Domain.Lending;

namespace DeviceRental.Application.Devices;

public static class DeviceCatalogError
{
    public const string OutsideAccessWindow = "OUTSIDE_ACCESS_WINDOW";
    public const string DeviceNotFound = "DEVICE_NOT_FOUND";
    public const string DeviceAlreadyBorrowed = "DEVICE_ALREADY_BORROWED";
    public const string DeviceUnavailable = "DEVICE_UNAVAILABLE";
    public const string ReturnNotAuthorized = "RETURN_NOT_AUTHORIZED";
    public const string NoActiveLoan = "NO_ACTIVE_LOAN";
    public const string ExtensionNotAuthorized = "EXTENSION_NOT_AUTHORIZED";
}

public sealed record RegisterDeviceCommand(
    string AssetNumber,
    string ModelName,
    DeviceTier Tier,
    Guid ImageId);

public sealed record DeviceSummary(
    Guid DeviceId,
    string AssetNumber,
    string ModelName,
    DeviceTier Tier,
    DeviceAvailability Availability,
    string? BorrowerName,
    Guid? BorrowerId,
    DateTimeOffset? DueAtUtc,
    string? UnavailableReason);

public sealed record LoanOperationView(
    Guid DeviceId,
    Guid LoanId,
    Guid BorrowerId,
    DateTimeOffset BorrowedAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? PreviousDueAtUtc = null);

public sealed record DeviceOperationResult<T>(
    bool Succeeded,
    string? ErrorCode,
    T? Value,
    DateTimeOffset? NextOpenUtc = null)
{
    public static DeviceOperationResult<T> Success(T value) => new(true, null, value);

    public static DeviceOperationResult<T> Failure(
        string errorCode,
        DateTimeOffset? nextOpenUtc = null) =>
        new(false, errorCode, default, nextOpenUtc);
}

/// <summary>
/// Coordinates the first vertical slice of device borrowing without leaking web or EF concerns
/// into the domain. The in-memory store is deliberately replaceable; production wiring can back
/// the same commands with a transactional repository while preserving these authorization and
/// state-transition rules.
/// </summary>
public sealed class DeviceCatalogService
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, CatalogEntry> _entries = new();
    private readonly AccessWindowPolicy _accessWindowPolicy;
    private readonly LoanExtensionPolicy _loanExtensionPolicy = new();
    private readonly Guid _policyVersionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private int _defaultLoanMinutes;

    public DeviceCatalogService(
        AccessWindowPolicy accessWindowPolicy,
        int defaultLoanMinutes = 1_440)
    {
        _accessWindowPolicy = accessWindowPolicy ?? throw new ArgumentNullException(nameof(accessWindowPolicy));
        if (defaultLoanMinutes is < 60 or > LoanExtensionPolicy.MaximumMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultLoanMinutes));
        }

        _defaultLoanMinutes = defaultLoanMinutes;
    }

    public IReadOnlyList<DeviceSummary> List(
        string? search = null,
        DeviceTier? tier = null,
        DeviceAvailability? availability = null)
    {
        lock (_gate)
        {
            var normalizedSearch = search?.Trim();
            return _entries.Values
                .Where(entry => !entry.Device.IsArchived)
                .Where(entry => tier is null || entry.Device.Tier == tier)
                .Select(ToSummary)
                .Where(summary => availability is null || summary.Availability == availability)
                .Where(summary => string.IsNullOrWhiteSpace(normalizedSearch) ||
                    summary.AssetNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    summary.ModelName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .OrderBy(summary => summary.AssetNumber, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public DeviceSummary? Get(Guid deviceId)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(deviceId, out var entry) ? ToSummary(entry) : null;
        }
    }

    public DeviceSummary Register(
        RegisterDeviceCommand command,
        Guid actorUserId,
        bool isAdministrator,
        DateTimeOffset effectiveNowUtc)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireAdministrator(isAdministrator);
        RequireOpenWindow(effectiveNowUtc);
        RequiredId(actorUserId, nameof(actorUserId));

        var device = new Device(
            Guid.NewGuid(),
            command.AssetNumber,
            command.ModelName,
            command.Tier,
            command.ImageId);
        lock (_gate)
        {
            if (_entries.Values.Any(entry =>
                    string.Equals(entry.Device.AssetNumber, device.AssetNumber, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The asset number is already registered.");
            }

            var entry = new CatalogEntry(device);
            _entries.Add(device.Id, entry);
            return ToSummary(entry);
        }
    }

    public DeviceOperationResult<LoanOperationView> Borrow(
        Guid deviceId,
        Guid borrowerId,
        string borrowerName,
        DateTimeOffset effectiveNowUtc)
    {
        var window = _accessWindowPolicy.Evaluate(effectiveNowUtc);
        if (!window.IsOpen)
        {
            return DeviceOperationResult<LoanOperationView>.Failure(
                DeviceCatalogError.OutsideAccessWindow,
                window.NextOpenUtc);
        }

        RequiredId(borrowerId, nameof(borrowerId));
        RequiredText(borrowerName, nameof(borrowerName));
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry) || entry.Device.IsArchived)
            {
                return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.DeviceNotFound);
            }

            if (entry.Loan is not null)
            {
                return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.DeviceAlreadyBorrowed);
            }

            if (!entry.Device.IsBorrowable(null))
            {
                return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.DeviceUnavailable);
            }

            var borrowedAtUtc = effectiveNowUtc.ToUniversalTime();
            var dueAtUtc = borrowedAtUtc.AddMinutes(_defaultLoanMinutes);
            var loan = Loan.Open(Guid.NewGuid(), deviceId, borrowerId, borrowedAtUtc, dueAtUtc, _policyVersionId);
            entry.Loan = loan;
            entry.BorrowerName = borrowerName.Trim();
            return DeviceOperationResult<LoanOperationView>.Success(ToLoanView(loan));
        }
    }

    public DeviceOperationResult<LoanOperationView> Return(
        Guid deviceId,
        Guid actorUserId,
        bool isAdministrator,
        DateTimeOffset effectiveNowUtc,
        string? reason)
    {
        var window = _accessWindowPolicy.Evaluate(effectiveNowUtc);
        if (!window.IsOpen)
        {
            return DeviceOperationResult<LoanOperationView>.Failure(
                DeviceCatalogError.OutsideAccessWindow,
                window.NextOpenUtc);
        }

        RequiredId(actorUserId, nameof(actorUserId));
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry) || entry.Loan is null)
            {
                return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.NoActiveLoan);
            }

            var loan = entry.Loan;
            if (loan.BorrowerId != actorUserId && !isAdministrator)
            {
                return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.ReturnNotAuthorized);
            }

            var kind = loan.BorrowerId == actorUserId ? ReturnKind.Self : ReturnKind.Forced;
            var returnReason = kind == ReturnKind.Forced ? Reason.From(reason ?? string.Empty) : null;
            var closedLoan = loan.Close(effectiveNowUtc, actorUserId, kind, returnReason);
            entry.Loan = null;
            entry.BorrowerName = null;
            return DeviceOperationResult<LoanOperationView>.Success(ToLoanView(closedLoan));
        }
    }

    public DeviceOperationResult<LoanOperationView> ForceReturnAndDisable(
        Guid deviceId,
        Guid actorUserId,
        bool isAdministrator,
        DateTimeOffset effectiveNowUtc,
        string reason)
    {
        var window = _accessWindowPolicy.Evaluate(effectiveNowUtc);
        if (!window.IsOpen)
        {
            return DeviceOperationResult<LoanOperationView>.Failure(
                DeviceCatalogError.OutsideAccessWindow,
                window.NextOpenUtc);
        }

        RequireAdministrator(isAdministrator);
        RequiredId(actorUserId, nameof(actorUserId));
        var normalizedReason = Reason.From(reason);
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry) || entry.Loan is null)
            {
                return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.NoActiveLoan);
            }

            var loan = entry.Loan;
            var closedLoan = loan.Close(effectiveNowUtc, actorUserId, ReturnKind.Forced, normalizedReason);
            entry.Loan = null;
            entry.BorrowerName = null;
            entry.Device = new Device(
                entry.Device.Id,
                entry.Device.AssetNumber,
                entry.Device.ModelName,
                entry.Device.Tier,
                entry.Device.ImageId,
                ManualDeviceState.TemporarilyDisabled,
                normalizedReason,
                entry.Device.IsArchived);
            return DeviceOperationResult<LoanOperationView>.Success(ToLoanView(closedLoan));
        }
    }

    public DeviceOperationResult<DeviceSummary> SetTemporarilyUnavailable(
        Guid deviceId,
        Guid actorUserId,
        bool isAdministrator,
        DateTimeOffset effectiveNowUtc,
        string reason)
    {
        var window = _accessWindowPolicy.Evaluate(effectiveNowUtc);
        if (!window.IsOpen)
        {
            return DeviceOperationResult<DeviceSummary>.Failure(
                DeviceCatalogError.OutsideAccessWindow,
                window.NextOpenUtc);
        }

        RequireAdministrator(isAdministrator);
        RequiredId(actorUserId, nameof(actorUserId));
        var normalizedReason = Reason.From(reason);
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry))
            {
                return DeviceOperationResult<DeviceSummary>.Failure(DeviceCatalogError.DeviceNotFound);
            }

            if (entry.Loan is not null)
            {
                return DeviceOperationResult<DeviceSummary>.Failure(DeviceCatalogError.DeviceAlreadyBorrowed);
            }

            entry.Device = new Device(
                entry.Device.Id,
                entry.Device.AssetNumber,
                entry.Device.ModelName,
                entry.Device.Tier,
                entry.Device.ImageId,
                ManualDeviceState.TemporarilyDisabled,
                normalizedReason,
                entry.Device.IsArchived);
            return DeviceOperationResult<DeviceSummary>.Success(ToSummary(entry));
        }
    }

    public DeviceOperationResult<DeviceSummary> RestoreAvailability(
        Guid deviceId,
        Guid actorUserId,
        bool isAdministrator,
        DateTimeOffset effectiveNowUtc)
    {
        var window = _accessWindowPolicy.Evaluate(effectiveNowUtc);
        if (!window.IsOpen)
        {
            return DeviceOperationResult<DeviceSummary>.Failure(
                DeviceCatalogError.OutsideAccessWindow,
                window.NextOpenUtc);
        }

        RequireAdministrator(isAdministrator);
        RequiredId(actorUserId, nameof(actorUserId));
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry))
            {
                return DeviceOperationResult<DeviceSummary>.Failure(DeviceCatalogError.DeviceNotFound);
            }

            if (entry.Loan is not null)
            {
                return DeviceOperationResult<DeviceSummary>.Failure(DeviceCatalogError.DeviceAlreadyBorrowed);
            }

            entry.Device = new Device(
                entry.Device.Id,
                entry.Device.AssetNumber,
                entry.Device.ModelName,
                entry.Device.Tier,
                entry.Device.ImageId,
                ManualDeviceState.Normal,
                null,
                entry.Device.IsArchived);
            return DeviceOperationResult<DeviceSummary>.Success(ToSummary(entry));
        }
    }

    public DeviceOperationResult<LoanOperationView> Extend(
        Guid deviceId,
        Guid actorUserId,
        bool isAdministrator,
        DurationMinutes duration,
        string reason,
        DateTimeOffset effectiveNowUtc)
    {
        var window = _accessWindowPolicy.Evaluate(effectiveNowUtc);
        if (!window.IsOpen)
        {
            return DeviceOperationResult<LoanOperationView>.Failure(
                DeviceCatalogError.OutsideAccessWindow,
                window.NextOpenUtc);
        }

        if (!isAdministrator)
        {
            return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.ExtensionNotAuthorized);
        }

        RequiredId(actorUserId, nameof(actorUserId));
        var normalizedReason = Reason.From(reason);
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry) || entry.Loan is null)
            {
                return DeviceOperationResult<LoanOperationView>.Failure(DeviceCatalogError.NoActiveLoan);
            }

            var result = _loanExtensionPolicy.Create(
                Guid.NewGuid(),
                entry.Loan,
                actorUserId,
                duration,
                normalizedReason,
                effectiveNowUtc);
            var previousDueAt = entry.Loan.DueAtUtc;
            entry.Loan = result.UpdatedLoan;
            return DeviceOperationResult<LoanOperationView>.Success(
                ToLoanView(entry.Loan, previousDueAt));
        }
    }

    public void SetDefaultLoanMinutes(
        int minutes,
        Guid actorUserId,
        bool isAdministrator,
        DateTimeOffset effectiveNowUtc,
        string reason)
    {
        var window = _accessWindowPolicy.Evaluate(effectiveNowUtc);
        if (!window.IsOpen)
        {
            throw new InvalidOperationException(DeviceCatalogError.OutsideAccessWindow);
        }

        RequireAdministrator(isAdministrator);
        RequiredId(actorUserId, nameof(actorUserId));
        _ = Reason.From(reason);
        if (minutes is < 60 or > LoanExtensionPolicy.MaximumMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes));
        }

        lock (_gate)
        {
            _defaultLoanMinutes = minutes;
        }
    }

    private static void RequireAdministrator(bool isAdministrator)
    {
        if (!isAdministrator)
        {
            throw new UnauthorizedAccessException("This operation requires the TEST_ADMIN role.");
        }
    }

    private static Guid RequiredId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }

        return value;
    }

    private static string RequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        }

        return value.Trim();
    }

    private void RequireOpenWindow(DateTimeOffset effectiveNowUtc)
    {
        if (!_accessWindowPolicy.Evaluate(effectiveNowUtc).IsOpen)
        {
            throw new InvalidOperationException(DeviceCatalogError.OutsideAccessWindow);
        }
    }

    private static DeviceSummary ToSummary(CatalogEntry entry)
    {
        var openLoan = entry.Loan?.IsOpen == true ? entry.Loan : null;
        var availability = entry.Device.GetAvailability(openLoan);
        return new DeviceSummary(
            entry.Device.Id,
            entry.Device.AssetNumber,
            entry.Device.ModelName,
            entry.Device.Tier,
            availability,
            openLoan is null ? null : entry.BorrowerName,
            openLoan?.BorrowerId,
            openLoan?.DueAtUtc,
            entry.Device.TemporaryUnavailableReason?.Value);
    }

    private static LoanOperationView ToLoanView(Loan loan, DateTimeOffset? previousDueAtUtc = null) =>
        new(loan.DeviceId, loan.Id, loan.BorrowerId, loan.BorrowedAtUtc, loan.DueAtUtc, previousDueAtUtc);

    private sealed class CatalogEntry(Device device)
    {
        public Device Device { get; set; } = device;

        public Loan? Loan { get; set; }

        public string? BorrowerName { get; set; }
    }
}
