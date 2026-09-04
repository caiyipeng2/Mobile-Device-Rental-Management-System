using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DeviceRental.Infrastructure.Notifications;

public interface IEmailTransport
{
    Task<NotificationSendResult> SendAsync(
        RenderedNotification message,
        CancellationToken cancellationToken = default);
}

public sealed class SmtpNotificationSender(
    INotificationPayloadCodec payloadCodec,
    INotificationTemplateRenderer templateRenderer,
    IEmailTransport transport) : INotificationSender
{
    public async Task<NotificationSendResult> SendAsync(
        OutboxClaim claim,
        CancellationToken cancellationToken = default)
    {
        NotificationPayload? payload = null;
        RenderedNotification message;
        try
        {
            payload = payloadCodec.Decode(claim);
            message = templateRenderer.Render(claim, payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return NotificationSendResult
                .PermanentFailure(SanitizeError(exception.Message))
                .WithDeliveryMetadata(claim.EventType, payload?.RecipientUserId);
        }

        try
        {
            var result = await transport.SendAsync(message, cancellationToken);
            return result.WithDeliveryMetadata(claim.EventType, payload.RecipientUserId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return NotificationSendResult
                .AcceptanceUnknown(SanitizeError(exception.Message))
                .WithDeliveryMetadata(claim.EventType, payload?.RecipientUserId);
        }
    }

    private static string SanitizeError(string error)
    {
        var normalized = string.IsNullOrWhiteSpace(error) ? "notification transport failed" : error.Trim();
        return normalized.Length <= 2_000 ? normalized : normalized[..2_000];
    }
}

public sealed class SystemNetMailTransport(IOptions<SmtpOptions> options) : IEmailTransport
{
    private readonly SmtpOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task<NotificationSendResult> SendAsync(
        RenderedNotification message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var mail = new System.Net.Mail.MailMessage(_options.FromAddress, message.RecipientEmail)
        {
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
        };
        using var client = new System.Net.Mail.SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseTls,
            Credentials = new System.Net.NetworkCredential(_options.Username, _options.Password),
            DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
        };
        try
        {
            await client.SendMailAsync(mail, cancellationToken);
            return NotificationSendResult.Accepted("smtp:accepted");
        }
        catch (System.Net.Mail.SmtpException exception) when ((int)exception.StatusCode is >= 400 and < 500)
        {
            return NotificationSendResult.TransientFailure($"SMTP {exception.StatusCode}");
        }
        catch (System.Net.Mail.SmtpException exception) when ((int)exception.StatusCode >= 500)
        {
            return NotificationSendResult.PermanentFailure($"SMTP {exception.StatusCode}");
        }
    }
}
