namespace DeviceRental.Domain.Notifications;

public enum OutboxStatus
{
    Pending,
    Claimed,
    Sending,
    Processed,
    DeadLetter,
    ReviewRequired,
    Cancelled,
}
