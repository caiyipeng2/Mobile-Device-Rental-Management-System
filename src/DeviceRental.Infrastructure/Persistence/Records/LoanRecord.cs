namespace DeviceRental.Infrastructure.Persistence.Records;

public sealed class LoanRecord
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Guid BorrowerId { get; set; }

    public DateTimeOffset BorrowedAt { get; set; }

    public DateTimeOffset DueAt { get; set; }

    public Guid PolicyVersionId { get; set; }

    public DateTimeOffset? ReturnedAt { get; set; }

    public Guid? ReturnedByUserId { get; set; }

    public string? ReturnKind { get; set; }

    public string? ReturnReason { get; set; }

    public long Version { get; set; }
}
