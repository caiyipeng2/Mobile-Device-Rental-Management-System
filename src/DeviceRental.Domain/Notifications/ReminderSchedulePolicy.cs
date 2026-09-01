using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Notifications;

public sealed record ReminderSchedule(
    DateTimeOffset? AdvanceReminderAtUtc,
    DateTimeOffset DueReminderAtUtc);

public sealed class ReminderSchedulePolicy
{
    private static readonly TimeSpan Advance = TimeSpan.FromHours(2);
    private static readonly TimeSpan MinimumLeadTime = TimeSpan.FromMinutes(5);

    public ReminderSchedule Create(DateTimeOffset createdAtUtc, DateTimeOffset dueAtUtc)
    {
        var normalizedCreatedAt = DomainGuard.Utc(createdAtUtc);
        var normalizedDueAt = DomainGuard.Utc(dueAtUtc);
        if (normalizedDueAt <= normalizedCreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(dueAtUtc), dueAtUtc, "Due time must follow creation.");
        }

        var candidate = normalizedDueAt - Advance;
        DateTimeOffset? advanceReminder = candidate - normalizedCreatedAt >= MinimumLeadTime
            ? candidate
            : null;
        return new ReminderSchedule(advanceReminder, normalizedDueAt);
    }
}
