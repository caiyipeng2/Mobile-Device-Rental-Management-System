using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DeviceRental.Infrastructure.Options;

public sealed class WorkerOptions
{
    [Range(1, 100)]
    public int BatchSize { get; set; } = 25;

    [Range(1, 3_600)]
    public int PollIntervalSeconds { get; set; } = 10;

    [Range(5, 86_400)]
    public int LeaseDurationSeconds { get; set; } = 120;

    public string WorkerId { get; set; } = string.Empty;
}

public sealed class WorkerOptionsValidator : IValidateOptions<WorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkerOptions options)
    {
        var failures = new List<string>();
        if (options.BatchSize is < 1 or > 100) failures.Add("Worker 批量大小必须在 1-100 之间。");
        if (options.PollIntervalSeconds is < 1 or > 3_600) failures.Add("Worker 轮询间隔必须在 1-3600 秒之间。");
        if (options.LeaseDurationSeconds is < 5 or > 86_400) failures.Add("Worker 租约时长必须在 5-86400 秒之间。");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
