using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;
using Xunit;

namespace DeviceRental.UnitTests.Lending;

public sealed class LoanEntityInvariantTests
{
    [Fact]
    public void LoanExtension_RejectsAnInconsistentDerivedTuple()
    {
        var effective = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
        var oldDue = effective.AddHours(1);

        Assert.Throws<ArgumentException>(() => new LoanExtension(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            oldDue,
            oldDue.AddHours(2),
            effective,
            DurationMinutes.From(60),
            Reason.From("reason")));
        Assert.Throws<ArgumentException>(() => new LoanExtension(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            oldDue,
            oldDue.AddHours(1),
            effective,
            DurationMinutes.From(60),
            Reason.From("reason")));
    }

    [Fact]
    public void LoanPolicyVersion_RequiresValidVersionDurationActorAndReason()
    {
        var policy = new LoanPolicyVersion(
            Guid.NewGuid(),
            1,
            DurationMinutes.From(1_440),
            new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(8)),
            Guid.NewGuid(),
            Reason.From("initial policy"));

        Assert.Equal(1_440, policy.Duration.Value);
        Assert.Equal(TimeSpan.Zero, policy.EffectiveAtUtc.Offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoanPolicyVersion(
            Guid.NewGuid(),
            0,
            DurationMinutes.From(1_440),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Reason.From("reason")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoanPolicyVersion(
            Guid.NewGuid(),
            1,
            DurationMinutes.From(59),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Reason.From("reason")));
        Assert.Throws<ArgumentException>(() => new LoanPolicyVersion(
            Guid.NewGuid(),
            1,
            DurationMinutes.From(60),
            DateTimeOffset.UtcNow,
            Guid.Empty,
            Reason.From("reason")));
    }
}
