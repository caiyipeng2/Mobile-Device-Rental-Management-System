using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Lending;

public sealed class LoanPolicyVersion
{
    public LoanPolicyVersion(
        Guid id,
        int versionNumber,
        DurationMinutes duration,
        DateTimeOffset effectiveAtUtc,
        Guid changedByUserId,
        Reason reason)
    {
        ArgumentNullException.ThrowIfNull(duration);
        ArgumentNullException.ThrowIfNull(reason);

        Id = DomainGuard.RequiredId(id, nameof(id));
        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), versionNumber, "Version number must be positive.");
        }

        if (duration.Value is < LoanExtensionPolicy.MinimumMinutes or > LoanExtensionPolicy.MaximumMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration.Value, "Loan duration is outside the approved range.");
        }

        VersionNumber = versionNumber;
        Duration = duration;
        EffectiveAtUtc = DomainGuard.Utc(effectiveAtUtc);
        ChangedByUserId = DomainGuard.RequiredId(changedByUserId, nameof(changedByUserId));
        Reason = reason;
    }

    public Guid Id { get; }

    public int VersionNumber { get; }

    public DurationMinutes Duration { get; }

    public DateTimeOffset EffectiveAtUtc { get; }

    public Guid ChangedByUserId { get; }

    public Reason Reason { get; }
}
