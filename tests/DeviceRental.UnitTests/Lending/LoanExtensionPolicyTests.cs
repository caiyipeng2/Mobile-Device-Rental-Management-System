using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;
using Xunit;

namespace DeviceRental.UnitTests.Lending;

public sealed class LoanExtensionPolicyTests
{
    private readonly LoanExtensionPolicy _policy = new();

    [Theory]
    [InlineData("2026-09-02T10:00:00Z", "2026-09-01T10:00:00Z", 60, "2026-09-02T11:00:00Z")]
    [InlineData("2026-09-01T09:00:00Z", "2026-09-01T10:00:00Z", 60, "2026-09-01T11:00:00Z")]
    [Trait("Requirement", "REQ-LOAN-012")]
    public void CalculateNewDueAt_UsesLaterOfOldDueAndEffectiveNow(
        string oldDue,
        string effectiveNow,
        int minutes,
        string expected)
    {
        var result = _policy.CalculateNewDueAt(
            DateTimeOffset.Parse(oldDue),
            DateTimeOffset.Parse(effectiveNow),
            DurationMinutes.From(minutes));

        Assert.Equal(DateTimeOffset.Parse(expected), result);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(10081)]
    public void CalculateNewDueAt_RejectsExtensionOutsideApprovedRange(int minutes)
    {
        var now = DateTimeOffset.Parse("2026-09-01T10:00:00Z");

        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.CalculateNewDueAt(
            now,
            now,
            DurationMinutes.From(minutes)));
    }

    [Fact]
    public void CalculateNewDueAt_RejectsResultLaterThanEffectiveNowPlusSevenDays()
    {
        var now = DateTimeOffset.Parse("2026-09-01T10:00:00Z");

        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.CalculateNewDueAt(
            now.AddDays(7).AddMinutes(-59),
            now,
            DurationMinutes.From(60)));
        Assert.Equal(
            now.AddDays(7),
            _policy.CalculateNewDueAt(now, now, DurationMinutes.From(10_080)));
    }

    [Fact]
    public void Create_ProducesAStrictExtensionTupleWithUtcValues()
    {
        var now = new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.FromHours(8));
        var loan = Loan.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            now.AddHours(-1),
            now.AddHours(1),
            Guid.NewGuid());

        var result = _policy.Create(
            Guid.NewGuid(),
            loan,
            Guid.NewGuid(),
            DurationMinutes.From(60),
            Reason.From("approved extension"),
            now);

        Assert.Equal(loan.DueAtUtc.AddHours(1), result.Extension.NewDueAtUtc);
        Assert.Equal(result.Extension.NewDueAtUtc, result.UpdatedLoan.DueAtUtc);
        Assert.Equal(TimeSpan.Zero, result.Extension.EffectiveAtUtc.Offset);
        Assert.Equal("approved extension", result.Extension.Reason.Value);
    }

    [Fact]
    public void Create_UsesTheUpdatedLoanForConsecutiveExtensionsAndStatus()
    {
        var now = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
        var original = Loan.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            now.AddHours(-1),
            now.AddHours(1),
            Guid.NewGuid());

        var first = _policy.Create(
            Guid.NewGuid(),
            original,
            Guid.NewGuid(),
            DurationMinutes.From(60),
            Reason.From("first extension"),
            now);
        var second = _policy.Create(
            Guid.NewGuid(),
            first.UpdatedLoan,
            Guid.NewGuid(),
            DurationMinutes.From(60),
            Reason.From("second extension"),
            now.AddMinutes(30));

        Assert.Equal(now.AddHours(2), first.UpdatedLoan.DueAtUtc);
        Assert.Equal(first.UpdatedLoan.DueAtUtc, second.Extension.OldDueAtUtc);
        Assert.Equal(now.AddHours(3), second.UpdatedLoan.DueAtUtc);
        Assert.Equal(LoanStatus.Active, second.UpdatedLoan.GetStatus(now.AddHours(2).AddMinutes(30)));
        Assert.Equal(LoanStatus.Overdue, second.UpdatedLoan.GetStatus(now.AddHours(3)));
    }

    [Fact]
    public void LoanExtensionResult_RequiresBothUpdatedLoanAndHistory()
    {
        var now = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
        var loan = Loan.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            now.AddHours(-1),
            now.AddHours(1),
            Guid.NewGuid());
        var extension = new LoanExtension(
            Guid.NewGuid(),
            loan.Id,
            Guid.NewGuid(),
            loan.DueAtUtc,
            loan.DueAtUtc.AddHours(1),
            now,
            DurationMinutes.From(60),
            Reason.From("extension"));

        Assert.Throws<ArgumentNullException>(() => new LoanExtensionResult(null!, extension));
        Assert.Throws<ArgumentNullException>(() => new LoanExtensionResult(loan, null!));
    }
}
