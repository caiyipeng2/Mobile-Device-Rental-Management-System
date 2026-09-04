using System.Security.Cryptography;
using System.Text;
using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeviceRental.WebTests.Notifications;

public sealed class NotificationTemplateTests
{
    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public void Borrowed_template_contains_device_and_due_time()
    {
        var payload = new NotificationPayload(
            "alice@example.com",
            "Alice",
            new Dictionary<string, string?>
            {
                ["deviceModel"] = "Pixel 9",
                ["assetNumber"] = "DEV-037",
                ["borrowedAt"] = "2026-09-04 10:00",
                ["dueAt"] = "2026-09-05 10:00",
            });
        var claim = CreateClaim("LOAN_BORROWED");

        var rendered = new NotificationTemplateRenderer().Render(claim, payload);

        Assert.Equal("alice@example.com", rendered.RecipientEmail);
        Assert.Contains("Pixel 9", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("DEV-037", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("2026-09-05 10:00", rendered.Body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-NOTIFY-003")]
    public void Forced_return_template_contains_the_administrator_reason()
    {
        var payload = new NotificationPayload(
            "alice@example.com",
            "Alice",
            new Dictionary<string, string?>
            {
                ["deviceModel"] = "Pixel 9",
                ["assetNumber"] = "DEV-037",
                ["reason"] = "屏幕破损，送修检查",
            });

        var rendered = new NotificationTemplateRenderer().Render(CreateClaim("LOAN_FORCED_RETURN"), payload);

        Assert.Contains("屏幕破损，送修检查", rendered.Body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    public void Unsupported_event_type_is_rejected_before_sending()
    {
        var payload = new NotificationPayload(
            "alice@example.com",
            "Alice",
            new Dictionary<string, string?>());

        Assert.Throws<InvalidOperationException>(() =>
            new NotificationTemplateRenderer().Render(CreateClaim("UNSUPPORTED"), payload));
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public void Encrypted_payload_round_trips_and_rejects_a_different_key_version()
    {
        var options = Options.Create(new NotificationEncryptionOptions
        {
            CurrentKeyVersion = "test-v1",
            CurrentKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });
        var codec = new AesGcmNotificationPayloadCodec(options);
        var payload = new NotificationPayload(
            "alice@example.com",
            "Alice",
            new Dictionary<string, string?> { ["verificationUrl"] = "https://desk.test/verify" });
        var ciphertext = codec.Encode(payload, 1);
        var claim = CreateClaim("ACCOUNT_EMAIL_VERIFICATION") with
        {
            PayloadSchemaVersion = 1,
            PayloadKeyVersion = "test-v1",
            PayloadCiphertext = ciphertext,
        };

        var decoded = codec.Decode(claim);

        Assert.Equal(payload.RecipientEmail, decoded.RecipientEmail);
        Assert.Equal(payload.Values["verificationUrl"], decoded.Values["verificationUrl"]);
        Assert.Throws<InvalidOperationException>(() => codec.Decode(claim with { PayloadKeyVersion = "old-v1" }));
    }

    private static OutboxClaim CreateClaim(string eventType) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        eventType,
        "LOAN",
        Guid.NewGuid().ToString("D"),
        1,
        "correlation-1",
        0,
        DateTimeOffset.Parse("2026-09-04T10:00:00Z"),
        1,
        "test-v1",
        Encoding.UTF8.GetBytes("placeholder"));
}
