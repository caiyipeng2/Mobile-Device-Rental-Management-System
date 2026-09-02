using DeviceRental.Domain.Notifications;

namespace DeviceRental.Application.Notifications;

public sealed record LoanNotificationPlan(
    Guid LoanId,
    long AggregateVersion,
    DateTimeOffset? AdvanceReminderAtUtc,
    DateTimeOffset DueReminderAtUtc,
    string? AdvanceReminderKey,
    string DueReminderKey);

/// <summary>
/// Produces notification timings and idempotency keys from one loan snapshot. The worker can
/// persist the resulting keys as outbox dedupe keys and is therefore free to retry delivery
/// without re-creating a reminder after a restart or a repeated command.
/// </summary>
public sealed class LoanNotificationPlanner
{
    private readonly ReminderSchedulePolicy _schedulePolicy = new();

    public LoanNotificationPlan Create(
        Guid loanId,
        long aggregateVersion,
        DateTimeOffset createdAtUtc,
        DateTimeOffset dueAtUtc)
    {
        if (loanId == Guid.Empty)
        {
            throw new ArgumentException("Loan identifier cannot be empty.", nameof(loanId));
        }

        if (aggregateVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aggregateVersion));
        }

        var schedule = _schedulePolicy.Create(createdAtUtc, dueAtUtc);
        var prefix = $"loan:{loanId:D}:v{aggregateVersion}";
        return new LoanNotificationPlan(
            loanId,
            aggregateVersion,
            schedule.AdvanceReminderAtUtc,
            schedule.DueReminderAtUtc,
            schedule.AdvanceReminderAtUtc is null ? null : $"{prefix}:advance",
            $"{prefix}:due");
    }
}
