using DeviceRental.Application.Notifications;

namespace DeviceRental.Infrastructure.Notifications;

public sealed class UnconfiguredNotificationOutboxWriter : INotificationOutboxWriter
{
    public void Enqueue(NotificationOutboxRequest request) =>
        throw new InvalidOperationException(
            "NotificationEncryption:CurrentKeyVersion and CurrentKeyBase64 must be configured before notification events can be written.");
}

public sealed class NoopNotificationOutboxWriter : INotificationOutboxWriter
{
    public void Enqueue(NotificationOutboxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
    }
}
