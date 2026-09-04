using DeviceRental.Application.Notifications;
using DeviceRental.Domain.Notifications;
using Xunit;

namespace DeviceRental.UnitTests.Notifications;

public sealed class OutboxProcessorTests
{
    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ProcessOnceAsync_MarksAcceptedMessageProcessed()
    {
        var now = DateTimeOffset.Parse("2026-09-03T10:00:00Z");
        var store = new FakeOutboxStore(CreateClaim());
        var processor = new OutboxProcessor(store, new FakeNotificationSender(
            NotificationSendResult.Accepted("smtp:250-accepted")));

        var summary = await processor.ProcessOnceAsync(now, "worker-a", 10, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.Processed);
        Assert.Equal(0, summary.Retried);
        Assert.Equal(1, store.ProcessedCount);
    }

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ProcessOnceAsync_SchedulesRetryForExplicitTransientRejection()
    {
        var now = DateTimeOffset.Parse("2026-09-03T10:00:00Z");
        var store = new FakeOutboxStore(CreateClaim());
        var processor = new OutboxProcessor(store, new FakeNotificationSender(
            NotificationSendResult.TransientFailure("smtp temporary rejection")));

        var summary = await processor.ProcessOnceAsync(now, "worker-a", 10, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.Retried);
        Assert.Equal(0, summary.DeadLettered);
        Assert.Equal("smtp temporary rejection", store.LastError);
    }

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ProcessOnceAsync_SendsPermanentRejectionToDeadLetter()
    {
        var store = new FakeOutboxStore(CreateClaim());
        var processor = new OutboxProcessor(store, new FakeNotificationSender(
            NotificationSendResult.PermanentFailure("smtp rejected recipient")));

        var summary = await processor.ProcessOnceAsync(
            DateTimeOffset.Parse("2026-09-03T10:00:00Z"),
            "worker-a",
            10,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.DeadLettered);
        Assert.Equal(0, summary.ManualReview);
        Assert.Equal(OutboxStatus.DeadLetter, store.FailureStatus);
    }

    [Fact]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ProcessOnceAsync_UsesManualReviewForUnknownAcceptance()
    {
        var store = new FakeOutboxStore(CreateClaim());
        var processor = new OutboxProcessor(store, new FakeNotificationSender(
            NotificationSendResult.AcceptanceUnknown("smtp timeout")));

        var summary = await processor.ProcessOnceAsync(
            DateTimeOffset.Parse("2026-09-03T10:00:00Z"),
            "worker-a",
            10,
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.ManualReview);
        Assert.Equal(OutboxStatus.ReviewRequired, store.FailureStatus);
    }

    private static OutboxClaim CreateClaim() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "LOAN_DUE",
        "LOAN",
        Guid.NewGuid().ToString("D"),
        1,
        "correlation-1",
        0,
        DateTimeOffset.Parse("2026-09-03T09:59:00Z"));

    private sealed class FakeNotificationSender(NotificationSendResult result) : INotificationSender
    {
        public Task<NotificationSendResult> SendAsync(OutboxClaim claim, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeOutboxStore(OutboxClaim claim) : IOutboxStore
    {
        public int ProcessedCount { get; private set; }

        public int RetriedCount { get; private set; }

        public string? LastError { get; private set; }

        public OutboxStatus? FailureStatus { get; private set; }

        public Task<IReadOnlyList<OutboxClaim>> ClaimDueAsync(
            DateTimeOffset effectiveNowUtc,
            string workerId,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OutboxClaim>>([claim]);

        public Task<bool> TryStartSendingAsync(
            Guid eventId,
            Guid leaseId,
            DateTimeOffset effectiveNowUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> MarkProcessedAsync(
            Guid eventId,
            Guid leaseId,
            DateTimeOffset effectiveNowUtc,
            CancellationToken cancellationToken = default)
        {
            ProcessedCount++;
            return Task.FromResult(true);
        }

        public Task<bool> ScheduleRetryAsync(
            Guid eventId,
            Guid leaseId,
            DateTimeOffset availableAtUtc,
            string sanitizedError,
            CancellationToken cancellationToken = default)
        {
            RetriedCount++;
            LastError = sanitizedError;
            return Task.FromResult(true);
        }

        public Task<bool> MarkFailedAsync(
            Guid eventId,
            Guid leaseId,
            OutboxStatus status,
            DateTimeOffset effectiveNowUtc,
            string sanitizedError,
            CancellationToken cancellationToken = default)
        {
            FailureStatus = status;
            LastError = sanitizedError;
            return Task.FromResult(true);
        }
    }
}
