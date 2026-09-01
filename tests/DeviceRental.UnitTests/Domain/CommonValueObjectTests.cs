using DeviceRental.Domain.Common;
using Xunit;

namespace DeviceRental.UnitTests.Domain;

public sealed class CommonValueObjectTests
{
    [Fact]
    public void Reason_TrimsAndRequiresNonWhitespaceWithoutInventingAMaximum()
    {
        Assert.Equal("maintenance", Reason.From("  maintenance  ").Value);
        Assert.Throws<ArgumentException>(() => Reason.From("   "));

        var longReason = new string('x', 20_000);
        Assert.Equal(longReason, Reason.From(longReason).Value);
    }

    [Fact]
    public void DurationMinutes_RequiresAPositiveWholeMinuteCount()
    {
        Assert.Equal(60, DurationMinutes.From(60).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => DurationMinutes.From(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DurationMinutes.From(-1));
    }
}
