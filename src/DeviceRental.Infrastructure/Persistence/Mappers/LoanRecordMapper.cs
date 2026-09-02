using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Persistence.Records;

namespace DeviceRental.Infrastructure.Persistence.Mappers;

public static class LoanRecordMapper
{
    public static LoanRecord ToRecord(Loan loan, long version = 1)
    {
        ArgumentNullException.ThrowIfNull(loan);
        return new LoanRecord
        {
            Id = loan.Id,
            DeviceId = loan.DeviceId,
            BorrowerId = loan.BorrowerId,
            BorrowedAt = loan.BorrowedAtUtc,
            DueAt = loan.DueAtUtc,
            PolicyVersionId = loan.PolicyVersionId,
            ReturnedAt = loan.ReturnedAtUtc,
            ReturnedByUserId = loan.ReturnedByUserId,
            ReturnKind = loan.ReturnKind switch
            {
                null => null,
                ReturnKind.Self => "SELF",
                ReturnKind.Forced => "FORCED",
                _ => throw new ArgumentOutOfRangeException(nameof(loan), loan.ReturnKind, "Unknown return kind."),
            },
            ReturnReason = loan.ReturnReason?.Value,
            Version = version,
        };
    }

    public static Loan ToDomain(LoanRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var loan = Loan.Open(
            record.Id,
            record.DeviceId,
            record.BorrowerId,
            record.BorrowedAt,
            record.DueAt,
            record.PolicyVersionId);
        if (record.ReturnedAt is null)
        {
            if (record.ReturnedByUserId is not null || record.ReturnKind is not null || record.ReturnReason is not null)
            {
                throw new ArgumentException("An open loan cannot contain return fields.", nameof(record));
            }

            return loan;
        }

        if (record.ReturnedByUserId is null)
        {
            throw new ArgumentException("A closed loan requires a returning user.", nameof(record));
        }

        var returnKind = record.ReturnKind switch
        {
            "SELF" => ReturnKind.Self,
            "FORCED" => ReturnKind.Forced,
            _ => throw new ArgumentOutOfRangeException(nameof(record), record.ReturnKind, "Unknown return kind."),
        };
        var reason = record.ReturnReason is null ? null : Reason.From(record.ReturnReason);
        return loan.Close(record.ReturnedAt.Value, record.ReturnedByUserId.Value, returnKind, reason);
    }
}
