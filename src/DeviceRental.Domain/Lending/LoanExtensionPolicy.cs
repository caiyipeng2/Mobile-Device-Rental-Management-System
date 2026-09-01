using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Lending;

public sealed class LoanExtensionResult
{
    public LoanExtensionResult(Loan updatedLoan, LoanExtension extension)
    {
        UpdatedLoan = updatedLoan ?? throw new ArgumentNullException(nameof(updatedLoan));
        Extension = extension ?? throw new ArgumentNullException(nameof(extension));
    }

    public Loan UpdatedLoan { get; }

    public LoanExtension Extension { get; }
}

public sealed class LoanExtensionPolicy
{
    public const int MinimumMinutes = 60;
    public const int MaximumMinutes = 10_080;

    public DateTimeOffset CalculateNewDueAt(
        DateTimeOffset oldDueAtUtc,
        DateTimeOffset effectiveNow,
        DurationMinutes duration)
    {
        ArgumentNullException.ThrowIfNull(duration);
        if (duration.Value is < MinimumMinutes or > MaximumMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration.Value, "Extension duration is outside the approved range.");
        }

        var normalizedOldDueAt = DomainGuard.Utc(oldDueAtUtc);
        var normalizedEffectiveNow = DomainGuard.Utc(effectiveNow);
        var extensionBase = normalizedOldDueAt > normalizedEffectiveNow
            ? normalizedOldDueAt
            : normalizedEffectiveNow;
        var newDueAt = extensionBase.AddMinutes(duration.Value);
        if (newDueAt > normalizedEffectiveNow.AddDays(7))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration.Value, "New due time cannot exceed seven days from the effective time.");
        }

        return newDueAt;
    }

    public LoanExtensionResult Create(
        Guid extensionId,
        Loan loan,
        Guid actorUserId,
        DurationMinutes duration,
        Reason reason,
        DateTimeOffset effectiveNow)
    {
        ArgumentNullException.ThrowIfNull(loan);
        if (!loan.IsOpen)
        {
            throw new InvalidOperationException("A returned loan cannot be extended.");
        }

        var normalizedEffectiveNow = DomainGuard.Utc(effectiveNow);
        var newDueAt = CalculateNewDueAt(loan.DueAtUtc, normalizedEffectiveNow, duration);
        var extension = new LoanExtension(
            extensionId,
            loan.Id,
            actorUserId,
            loan.DueAtUtc,
            newDueAt,
            normalizedEffectiveNow,
            duration,
            reason);
        return new LoanExtensionResult(loan.ExtendDueAt(newDueAt), extension);
    }
}
