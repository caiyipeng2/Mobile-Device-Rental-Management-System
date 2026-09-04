using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DeviceRental.Infrastructure.Notifications;

public sealed class AesGcmNotificationPayloadCodec : INotificationPayloadCodec
{
    private const int CurrentSchemaVersion = 1;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private readonly NotificationEncryptionOptions _options;

    public AesGcmNotificationPayloadCodec(IOptions<NotificationEncryptionOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.CurrentKeyVersion))
        {
            throw new InvalidOperationException("Notification encryption key version is required.");
        }

        var key = DecodeKey(_options.CurrentKeyBase64);
        if (key.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException("Notification encryption key must be 128, 192, or 256 bits.");
        }
    }

    public byte[] Encode(NotificationPayload payload, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Unsupported notification payload schema.");
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var tag = new byte[TagLength];
        var ciphertext = new byte[plaintext.Length];
        using var aes = new AesGcm(DecodeKey(_options.CurrentKeyBase64), TagLength);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return [.. nonce, .. tag, .. ciphertext];
    }

    public NotificationPayload Decode(OutboxClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (claim.PayloadSchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException("Unsupported notification payload schema.");
        }

        if (!string.Equals(claim.PayloadKeyVersion, _options.CurrentKeyVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Notification payload key version is not available.");
        }

        if (claim.PayloadCiphertext.Length < NonceLength + TagLength)
        {
            throw new InvalidOperationException("Notification payload ciphertext is incomplete.");
        }

        var nonce = claim.PayloadCiphertext[..NonceLength];
        var tag = claim.PayloadCiphertext[NonceLength..(NonceLength + TagLength)];
        var ciphertext = claim.PayloadCiphertext[(NonceLength + TagLength)..];
        var plaintext = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(DecodeKey(_options.CurrentKeyBase64), TagLength);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            var payload = JsonSerializer.Deserialize<NotificationPayload>(plaintext)
                ?? throw new InvalidOperationException("Notification payload is empty.");
            if (string.IsNullOrWhiteSpace(payload.RecipientEmail) || string.IsNullOrWhiteSpace(payload.RecipientDisplayName))
            {
                throw new InvalidOperationException("Notification payload recipient is incomplete.");
            }

            return payload;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Notification payload authentication failed.", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Notification payload format is invalid.", exception);
        }
    }

    private static byte[] DecodeKey(string? keyBase64)
    {
        try
        {
            return Convert.FromBase64String(keyBase64 ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Notification encryption key must be valid base64.", exception);
        }
    }
}
