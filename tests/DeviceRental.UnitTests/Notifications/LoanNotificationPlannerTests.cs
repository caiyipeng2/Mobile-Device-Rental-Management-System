using DeviceRental.Application.Notifications;
using Xunit;

namespace DeviceRental.UnitTests.Notifications;

public sealed class LoanNotificationPlannerTests
{
    private static readonly Guid LoanId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-09-01T02:00:00Z");

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-002")]
    public void Plan_contains_advance_and_due_reminders_when_lead_time_is_sufficient()
    {
        var planner = new LoanNotificationPlanner();
        var plan = planner.Create(LoanId, aggregateVersion: 1, CreatedAt, CreatedAt.AddHours(4));

        Assert.Equal(CreatedAt.AddHours(2), plan.AdvanceReminderAtUtc);
        Assert.Equal(CreatedAt.AddHours(4), plan.DueReminderAtUtc);
        Assert.Equal("loan:40000000-0000-0000-0000-000000000001:v1:due", plan.DueReminderKey);
    }

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-002")]
    public void Plan_skips_advance_reminder_when_loan_is_created_too_close_to_due_time()
    {
        var planner = new LoanNotificationPlanner();
        var plan = planner.Create(LoanId, aggregateVersion: 2, CreatedAt, CreatedAt.AddMinutes(124));

        Assert.Null(plan.AdvanceReminderAtUtc);
        Assert.Equal(CreatedAt.AddMinutes(124), plan.DueReminderAtUtc);
        Assert.Equal("loan:40000000-0000-0000-0000-000000000001:v2:due", plan.DueReminderKey);
    }

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public void Keys_are_stable_and_change_when_the_loan_version_changes()
    {
        var planner = new LoanNotificationPlanner();
        var first = planner.Create(LoanId, aggregateVersion: 3, CreatedAt, CreatedAt.AddHours(5));
        var second = planner.Create(LoanId, aggregateVersion: 4, CreatedAt, CreatedAt.AddHours(5));

        Assert.NotEqual(first.DueReminderKey, second.DueReminderKey);
        Assert.Equal(first.AdvanceReminderKey!.Replace("v3", "v4"), second.AdvanceReminderKey);
    }
}
