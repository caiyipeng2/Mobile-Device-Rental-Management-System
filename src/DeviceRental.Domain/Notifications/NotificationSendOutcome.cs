namespace DeviceRental.Domain.Notifications;

public enum NotificationSendOutcome
{
    Accepted,
    TransientNotAccepted,
    PermanentRejected,
    AcceptanceUnknown,
}
