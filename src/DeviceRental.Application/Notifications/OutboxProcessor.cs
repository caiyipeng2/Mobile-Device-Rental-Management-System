using DeviceRental.Domain.Notifications;

namespace DeviceRental.Application.Notifications;

public sealed record OutboxProcessSummary(
    int Claimed,
    int Started,
    int Processed,
    int Retried,
    int DeadLettered,
    int ManualReview);

/// <summary>
/// Coordinates short database state transitions around an external sender. The sender is awaited
/// only after the SENDING CAS has committed, so SMTP latency never holds a database transaction.
/// </summary>
public sealed class OutboxProcessor(
    IOutboxStore store,
    INotificationSender sender)
{
    public async Task<OutboxProcessSummary> ProcessOnceAsync(
        DateTimeOffset effectiveNowUtc,
        string workerId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = effectiveNowUtc.ToUniversalTime();
        var claims = await store.ClaimDueAsync(
            now,
            workerId,
            batchSize,
            leaseDuration,
            cancellationToken);
        var started = 0;
        var processed = 0;
        var retried = 0;
        var deadLettered = 0;
        var manualReview = 0;

        foreach (var claim in claims)
        {
            if (!await store.TryStartSendingAsync(claim.EventId, claim.LeaseId, now, cancellationToken))
            {
                continue;
            }

            started++;
            var deliveryStartedAtUtc = DateTimeOffset.UtcNow;
            var result = await SendSafelyAsync(claim, cancellationToken);
            var deliveryCompletedAtUtc = DateTimeOffset.UtcNow;
            await store.RecordDeliveryAsync(
                claim,
                result,
                deliveryStartedAtUtc,
                deliveryCompletedAtUtc,
                cancellationToken);
            var disposition = DeliveryFailureClassifier.Classify(result.Outcome, result.AcceptanceEvidence);
            switch (disposition)
            {
                case DeliveryFailureDisposition.None:
                    if (await store.MarkProcessedAsync(claim.EventId, claim.LeaseId, now, cancellationToken))
                    {
                        processed++;
                    }

                    break;
                case DeliveryFailureDisposition.Retry:
                    if (await store.ScheduleRetryAsync(
                            claim.EventId,
                            claim.LeaseId,
                            now.Add(GetRetryDelay(claim.AttemptCount + 1)),
                            SanitizeError(result.SanitizedError),
                            cancellationToken))
                    {
                        retried++;
                    }

                    break;
                case DeliveryFailureDisposition.DeadLetter:
                    if (await store.MarkFailedAsync(
                            claim.EventId,
                            claim.LeaseId,
                            OutboxStatus.DeadLetter,
                            now,
                            SanitizeError(result.SanitizedError),
                            cancellationToken))
                    {
                        deadLettered++;
                    }

                    break;
                case DeliveryFailureDisposition.ManualReview:
                    if (await store.MarkFailedAsync(
                            claim.EventId,
                            claim.LeaseId,
                            OutboxStatus.ReviewRequired,
                            now,
                            SanitizeError(result.SanitizedError),
                            cancellationToken))
                    {
                        manualReview++;
                    }

                    break;
            }
        }

        return new OutboxProcessSummary(claims.Count, started, processed, retried, deadLettered, manualReview);
    }

    private async Task<NotificationSendResult> SendSafelyAsync(
        OutboxClaim claim,
        CancellationToken cancellationToken)
    {
        try
        {
            return await sender.SendAsync(claim, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return NotificationSendResult.AcceptanceUnknown(SanitizeError(exception.Message));
        }
    }

    private static TimeSpan GetRetryDelay(int attempt) =>
        TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Clamp(attempt, 1, 8)), 256));

    private static string SanitizeError(string? error)
    {
        var normalized = string.IsNullOrWhiteSpace(error) ? "notification delivery failed" : error.Trim();
        return normalized.Length <= 2_000 ? normalized : normalized[..2_000];
    }
}
