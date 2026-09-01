using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;
using Xunit;

namespace DeviceRental.UnitTests.Lending;

public sealed class LoanStatusPolicyTests
{
    private static readonly DateTimeOffset BorrowedAt = DateTimeOffset.Parse("2026-09-01T02:00:00Z");

    [Fact]
    public void ReturnKind_ContainsOnlySelfAndForced()
    {
        Assert.Equal(["Self", "Forced"], Enum.GetNames<ReturnKind>());
    }

    [Fact]
    [Trait("Requirement", "REQ-LOAN-011")]
    public void GetStatus_DerivesActiveOverdueAndReturnedWithoutMutableFlags()
    {
        var loan = OpenLoan();

        Assert.Equal(LoanStatus.Active, loan.GetStatus(loan.DueAtUtc.AddTicks(-1)));
        Assert.Equal(LoanStatus.Overdue, loan.GetStatus(loan.DueAtUtc));

        var returned = loan.Close(
            loan.DueAtUtc.AddHours(1),
            loan.BorrowerId,
            ReturnKind.Self,
            null);
        Assert.Equal(LoanStatus.Returned, returned.GetStatus(loan.DueAtUtc.AddDays(1)));
    }

    [Fact]
    public void Open_NormalizesUtcAndRejectsInvalidTupleMembers()
    {
        var loan = Loan.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.FromHours(8)),
            Guid.NewGuid());

        Assert.Equal(TimeSpan.Zero, loan.BorrowedAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, loan.DueAtUtc.Offset);
        Assert.Throws<ArgumentException>(() => Loan.Open(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            BorrowedAt,
            BorrowedAt.AddHours(1),
            Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Loan.Open(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            BorrowedAt,
            BorrowedAt,
            Guid.NewGuid()));
    }

    [Fact]
    public void Close_EnforcesReturnTupleAndReasonRules()
    {
        var loan = OpenLoan();

        Assert.Throws<ArgumentException>(() => loan.Close(
            loan.BorrowedAtUtc.AddMinutes(1),
            Guid.Empty,
            ReturnKind.Self,
            null));
        Assert.Throws<ArgumentException>(() => loan.Close(
            loan.BorrowedAtUtc.AddMinutes(1),
            Guid.NewGuid(),
            ReturnKind.Forced,
            null));
        Assert.Throws<ArgumentException>(() => loan.Close(
            loan.BorrowedAtUtc.AddMinutes(1),
            loan.BorrowerId,
            ReturnKind.Self,
            Reason.From("not allowed")));

        var forced = loan.Close(
            loan.BorrowedAtUtc.AddMinutes(1),
            Guid.NewGuid(),
            ReturnKind.Forced,
            Reason.From("broken display"));

        Assert.NotNull(forced.ReturnedAtUtc);
        Assert.Equal(ReturnKind.Forced, forced.ReturnKind);
        Assert.Equal("broken display", forced.ReturnReason!.Value);
        Assert.Throws<InvalidOperationException>(() => forced.Close(
            forced.ReturnedAtUtc!.Value,
            Guid.NewGuid(),
            ReturnKind.Forced,
            Reason.From("again")));
    }

    private static Loan OpenLoan() => Loan.Open(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        BorrowedAt,
        BorrowedAt.AddHours(1),
        Guid.NewGuid());
}
