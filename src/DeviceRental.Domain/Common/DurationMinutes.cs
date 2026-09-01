namespace DeviceRental.Domain.Common;

public sealed class DurationMinutes : IEquatable<DurationMinutes>
{
    private DurationMinutes(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static DurationMinutes From(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Duration must be positive.");
        }

        return new DurationMinutes(value);
    }

    public TimeSpan ToTimeSpan() => TimeSpan.FromMinutes(Value);

    public bool Equals(DurationMinutes? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is DurationMinutes other && Equals(other);

    public override int GetHashCode() => Value;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
