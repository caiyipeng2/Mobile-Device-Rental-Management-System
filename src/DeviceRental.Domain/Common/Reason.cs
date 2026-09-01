namespace DeviceRental.Domain.Common;

public sealed class Reason : IEquatable<Reason>
{
    private Reason(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Reason From(string value) => new(DomainGuard.RequiredText(value, nameof(value)));

    public bool Equals(Reason? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Reason other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
