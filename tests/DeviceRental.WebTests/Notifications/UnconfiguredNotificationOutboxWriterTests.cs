using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Notifications;
using Xunit;

namespace DeviceRental.WebTests.Notifications;

public sealed class UnconfiguredNotificationOutboxWriterTests
{
    [Fact]
    [Trait("Category", "Web")]
    public void Missing_encryption_configuration_fails_closed_on_event_write()
    {
        var writer = new UnconfiguredNotificationOutboxWriter();

        Assert.Throws<InvalidOperationException>(() => writer.Enqueue(new NotificationOutboxRequest(
            "event:1",
            "LOAN_BORROWED",
            "LOAN",
            Guid.NewGuid().ToString("D"),
            1,
            "correlation-1",
            new NotificationPayload(
                "alice@example.com",
                "Alice",
                new Dictionary<string, string?>()),
            DateTimeOffset.UtcNow)));
    }
}
