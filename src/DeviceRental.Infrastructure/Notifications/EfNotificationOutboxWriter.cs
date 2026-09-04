using DeviceRental.Application.Notifications;
using DeviceRental.Domain.Notifications;
using DeviceRental.Infrastructure.Options;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using Microsoft.Extensions.Options;

namespace DeviceRental.Infrastructure.Notifications;

/// <summary>
/// Adds an encrypted notification row to the caller's current DbContext transaction. It never
/// calls SaveChanges, so account and lending state commit or roll back with the event atomically.
/// </summary>
public sealed class EfNotificationOutboxWriter(
    DeviceRentalDbContext dbContext,
    INotificationPayloadCodec payloadCodec,
    IOptions<NotificationEncryptionOptions> options) : INotificationOutboxWriter
{
    private readonly NotificationEncryptionOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public void Enqueue(NotificationOutboxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Payload);
        var createdAt = request.CreatedAtUtc.ToUniversalTime();
        var payload = new EncryptedPayload(
            1,
            _options.CurrentKeyVersion,
            payloadCodec.Encode(request.Payload, 1));
        var message = OutboxMessage.Pending(
            Guid.NewGuid(),
            request.DeduplicationKey,
            request.EventType,
            request.AggregateType,
            request.AggregateId,
            request.AggregateVersion,
            request.CorrelationId,
            payload,
            createdAt,
            request.AvailableAtUtc?.ToUniversalTime() ?? createdAt);
        dbContext.OutboxMessages.Add(OutboxMessageMapper.ToRecord(message));
    }
}
