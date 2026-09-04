using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DeviceRental.Worker;

public sealed class OutboxWorker(
    OutboxProcessor processor,
    IOptions<WorkerOptions> options,
    ILogger<OutboxWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var workerId = string.IsNullOrWhiteSpace(settings.WorkerId)
            ? Environment.MachineName
            : settings.WorkerId.Trim();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var summary = await processor.ProcessOnceAsync(
                    timeProvider.GetUtcNow(),
                    workerId,
                    settings.BatchSize,
                    TimeSpan.FromSeconds(settings.LeaseDurationSeconds),
                    stoppingToken);
                if (summary.Claimed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Outbox batch processed. Claimed={Claimed} Started={Started} Processed={Processed} Retried={Retried} DeadLettered={DeadLettered} ManualReview={ManualReview}",
                        summary.Claimed,
                        summary.Started,
                        summary.Processed,
                        summary.Retried,
                        summary.DeadLettered,
                        summary.ManualReview);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox polling failed; the Worker will retry on the next interval.");
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }
}
