using System.Security.Cryptography;
using DeviceRental.Application.Notifications;
using DeviceRental.Domain.Notifications;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeviceRental.WebTests.Notifications;

public sealed class SmtpNotificationSenderTests
{
    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public async Task Sender_decodes_and_renders_payload_before_transport()
    {
        var options = CreateOptions();
        var codec = new AesGcmNotificationPayloadCodec(options);
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
        var claim = CreateClaim("LOAN_BORROWED", codec.Encode(payload, 1));
        var transport = new FakeEmailTransport(NotificationSendResult.Accepted("smtp:250"));
        var sender = new SmtpNotificationSender(
            codec,
            new NotificationTemplateRenderer(),
            transport);

        var result = await sender.SendAsync(claim, TestContext.Current.CancellationToken);

        Assert.Equal(NotificationSendOutcome.Accepted, result.Outcome);
        Assert.Equal("alice@example.com", transport.Message!.RecipientEmail);
        Assert.Contains("Pixel 9", transport.Message.Body, StringComparison.Ordinal);
        Assert.Contains("测试设备借用成功", transport.Message.Subject, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task Sender_turns_transport_exception_into_unknown_acceptance()
    {
        var options = CreateOptions();
        var codec = new AesGcmNotificationPayloadCodec(options);
        var recipientId = Guid.NewGuid();
        var payload = new NotificationPayload(
            "alice@example.com",
            "Alice",
            new Dictionary<string, string?>
            {
                ["deviceModel"] = "Pixel 9",
                ["assetNumber"] = "DEV-037",
                ["dueAt"] = "2026-09-05 10:00",
            },
            recipientId);
        var transport = new FakeEmailTransport(new InvalidOperationException("socket timeout"));
        var sender = new SmtpNotificationSender(codec, new NotificationTemplateRenderer(), transport);

        var result = await sender.SendAsync(
            CreateClaim("LOAN_DUE", codec.Encode(payload, 1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationSendOutcome.AcceptanceUnknown, result.Outcome);
        Assert.Equal(SmtpAcceptanceEvidence.Unknown, result.AcceptanceEvidence);
        Assert.Equal(recipientId, result.RecipientUserId);
        Assert.Equal("LOAN_DUE", result.TemplateIdentifier);
        Assert.Contains("socket timeout", result.SanitizedError, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task Sender_turns_invalid_payload_into_permanent_rejection()
    {
        var options = CreateOptions();
        var sender = new SmtpNotificationSender(
            new AesGcmNotificationPayloadCodec(options),
            new NotificationTemplateRenderer(),
            new FakeEmailTransport(NotificationSendResult.Accepted("smtp:250")));

        var result = await sender.SendAsync(
            CreateClaim("LOAN_DUE", [1, 2, 3]),
            TestContext.Current.CancellationToken);

        Assert.Equal(NotificationSendOutcome.PermanentRejected, result.Outcome);
        Assert.Equal(SmtpAcceptanceEvidence.NotAccepted, result.AcceptanceEvidence);
    }

    [Fact]
    [Trait("Category", "Web")]
    public void Smtp_options_require_tls_and_credentials()
    {
        var validator = new SmtpOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new SmtpOptions { Host = "smtp.example.com", Port = 25, UseTls = false });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, message => message.Contains("TLS", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, message => message.Contains("发件邮箱", StringComparison.Ordinal));
    }

    private static IOptions<NotificationEncryptionOptions> CreateOptions() =>
        Options.Create(new NotificationEncryptionOptions
        {
            CurrentKeyVersion = "test-v1",
            CurrentKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });

    private static OutboxClaim CreateClaim(string eventType, byte[] ciphertext) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "notification:smtp-1",
        eventType,
        "LOAN",
        Guid.NewGuid().ToString("D"),
        1,
        "correlation-1",
        0,
        DateTimeOffset.Parse("2026-09-04T10:00:00Z"),
        1,
        "test-v1",
        ciphertext);

    private sealed class FakeEmailTransport(NotificationSendResult result) : IEmailTransport
    {
        public RenderedNotification? Message { get; private set; }

        private readonly Exception? _exception;

        public Task<NotificationSendResult> SendAsync(
            RenderedNotification message,
            CancellationToken cancellationToken = default)
        {
            Message = message;
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(result);
        }

        public FakeEmailTransport(Exception exception)
            : this(NotificationSendResult.AcceptanceUnknown(exception.Message))
        {
            _exception = exception;
        }

    }
}
