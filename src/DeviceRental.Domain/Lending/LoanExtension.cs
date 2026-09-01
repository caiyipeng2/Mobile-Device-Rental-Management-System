using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Lending;

public sealed class LoanExtension
{
    public LoanExtension(
        Guid id,
        Guid loanId,
        Guid actorUserId,
        DateTimeOffset oldDueAtUtc,
        DateTimeOffset newDueAtUtc,
        DateTimeOffset effectiveAtUtc,
        DurationMinutes duration,
        Reason reason)
    {
        ArgumentNullException.ThrowIfNull(duration);
        ArgumentNullException.ThrowIfNull(reason);

        Id = DomainGuard.RequiredId(id, nameof(id));
        LoanId = DomainGuard.RequiredId(loanId, nameof(loanId));
        ActorUserId = DomainGuard.RequiredId(actorUserId, nameof(actorUserId));
        OldDueAtUtc = DomainGuard.Utc(oldDueAtUtc);
        EffectiveAtUtc = DomainGuard.Utc(effectiveAtUtc);
        NewDueAtUtc = DomainGuard.Utc(newDueAtUtc);

        if (duration.Value is < LoanExtensionPolicy.MinimumMinutes or > LoanExtensionPolicy.MaximumMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration.Value, "Extension duration is outside the approved range.");
        }

        var expectedDueAt = (OldDueAtUtc > EffectiveAtUtc ? OldDueAtUtc : EffectiveAtUtc)
            .AddMinutes(duration.Value);
        if (NewDueAtUtc != expectedDueAt)
        {
            throw new ArgumentException("New due time does not match the extension tuple.", nameof(newDueAtUtc));
        }

        if (NewDueAtUtc > EffectiveAtUtc.AddDays(7))
        {
            throw new ArgumentOutOfRangeException(nameof(newDueAtUtc), newDueAtUtc, "New due time cannot exceed seven days from the effective time.");
        }

        Duration = duration;
        Reason = reason;
    }

    public Guid Id { get; }

    public Guid LoanId { get; }

    public Guid ActorUserId { get; }

    public DateTimeOffset OldDueAtUtc { get; }

    public DateTimeOffset NewDueAtUtc { get; }

    public DateTimeOffset EffectiveAtUtc { get; }

    public DurationMinutes Duration { get; }

    public Reason Reason { get; }
}
