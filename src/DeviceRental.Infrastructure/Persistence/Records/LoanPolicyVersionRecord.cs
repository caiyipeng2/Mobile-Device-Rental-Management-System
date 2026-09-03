namespace DeviceRental.Infrastructure.Persistence.Records;

public sealed class LoanPolicyVersionRecord
{
    public Guid Id { get; set; }

    public int VersionNumber { get; set; }

    public int DurationMinutes { get; set; }

    public DateTimeOffset EffectiveAtUtc { get; set; }

    public Guid ChangedByUserId { get; set; }

    public string Reason { get; set; } = string.Empty;
}
