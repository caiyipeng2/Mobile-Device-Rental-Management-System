using DeviceRental.Domain.Notifications;
using Xunit;

namespace DeviceRental.UnitTests.Notifications;

public sealed class ReminderSchedulePolicyTests
{
    private readonly ReminderSchedulePolicy _policy = new();

    [Theory]
    [InlineData(2, 4, 59, false)]
    [InlineData(2, 5, 0, true)]
    [Trait("Requirement", "REQ-NOTIFY-002")]
    public void Create_UsesInclusiveFiveMinuteAdvanceReminderBoundary(
        int hours,
        int minutes,
        int seconds,
        bool expectAdvance)
    {
        var createdAt = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
        var dueAt = createdAt.Add(new TimeSpan(hours, minutes, seconds));

        var schedule = _policy.Create(createdAt, dueAt);

        Assert.Equal(expectAdvance, schedule.AdvanceReminderAtUtc.HasValue);
        Assert.Equal(dueAt, schedule.DueReminderAtUtc);
        if (expectAdvance)
        {
            Assert.Equal(createdAt.AddMinutes(5), schedule.AdvanceReminderAtUtc);
        }
    }

    [Fact]
    public void Create_NormalizesUtcAndRequiresFutureDueTime()
    {
        var createdAt = new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.FromHours(8));
        var schedule = _policy.Create(createdAt, createdAt.AddHours(24));

        Assert.Equal(TimeSpan.Zero, schedule.DueReminderAtUtc.Offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.Create(createdAt, createdAt));
    }
}
