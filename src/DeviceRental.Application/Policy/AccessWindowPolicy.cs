namespace DeviceRental.Application.Policy;

public sealed record AccessWindowDecision(bool IsOpen, DateTimeOffset? NextOpenUtc);

public sealed class AccessWindowPolicy
{
    private static readonly TimeOnly OpensAt = new(9, 0);
    private static readonly TimeOnly ClosesAt = new(19, 0);
    private readonly TimeZoneInfo _shanghaiTimeZone;

    public AccessWindowPolicy()
        : this(ResolveShanghaiTimeZone())
    {
    }

    internal AccessWindowPolicy(TimeZoneInfo shanghaiTimeZone)
    {
        _shanghaiTimeZone = shanghaiTimeZone ?? throw new ArgumentNullException(nameof(shanghaiTimeZone));
    }

    public AccessWindowDecision Evaluate(DateTimeOffset utcNow)
    {
        var normalizedUtcNow = utcNow.ToUniversalTime();
        var shanghaiNow = TimeZoneInfo.ConvertTime(normalizedUtcNow, _shanghaiTimeZone);
        var localTime = TimeOnly.FromDateTime(shanghaiNow.DateTime);
        if (localTime >= OpensAt && localTime < ClosesAt)
        {
            return new AccessWindowDecision(true, null);
        }

        var localDate = DateOnly.FromDateTime(shanghaiNow.DateTime);
        if (localTime >= ClosesAt)
        {
            localDate = localDate.AddDays(1);
        }

        var nextOpenLocal = localDate.ToDateTime(OpensAt, DateTimeKind.Unspecified);
        var nextOpenUtc = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(nextOpenLocal, _shanghaiTimeZone),
            TimeSpan.Zero);
        return new AccessWindowDecision(false, nextOpenUtc);
    }

    private static TimeZoneInfo ResolveShanghaiTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        }
    }
}
