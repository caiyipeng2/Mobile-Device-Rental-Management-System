using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Lending;

public enum LoanStatus
{
    Active,
    Overdue,
    Returned,
}

public sealed class Loan
{
    private Loan(
        Guid id,
        Guid deviceId,
        Guid borrowerId,
        DateTimeOffset borrowedAtUtc,
        DateTimeOffset dueAtUtc,
        Guid policyVersionId,
        DateTimeOffset? returnedAtUtc,
        Guid? returnedByUserId,
        ReturnKind? returnKind,
        Reason? returnReason)
    {
        Id = DomainGuard.RequiredId(id, nameof(id));
        DeviceId = DomainGuard.RequiredId(deviceId, nameof(deviceId));
        BorrowerId = DomainGuard.RequiredId(borrowerId, nameof(borrowerId));
        PolicyVersionId = DomainGuard.RequiredId(policyVersionId, nameof(policyVersionId));
        BorrowedAtUtc = DomainGuard.Utc(borrowedAtUtc);
        DueAtUtc = DomainGuard.Utc(dueAtUtc);

        if (DueAtUtc <= BorrowedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(dueAtUtc), dueAtUtc, "Due time must follow borrow time.");
        }

        ValidateReturnTuple(returnedAtUtc, returnedByUserId, returnKind, returnReason);
        ReturnedAtUtc = returnedAtUtc is null ? null : DomainGuard.Utc(returnedAtUtc.Value);
        ReturnedByUserId = returnedByUserId;
        ReturnKind = returnKind;
        ReturnReason = returnReason;
    }

    public Guid Id { get; }

    public Guid DeviceId { get; }

    public Guid BorrowerId { get; }

    public DateTimeOffset BorrowedAtUtc { get; }

    public DateTimeOffset DueAtUtc { get; }

    public Guid PolicyVersionId { get; }

    public DateTimeOffset? ReturnedAtUtc { get; }

    public Guid? ReturnedByUserId { get; }

    public ReturnKind? ReturnKind { get; }

    public Reason? ReturnReason { get; }

    public bool IsOpen => ReturnedAtUtc is null;

    public static Loan Open(
        Guid id,
        Guid deviceId,
        Guid borrowerId,
        DateTimeOffset borrowedAtUtc,
        DateTimeOffset dueAtUtc,
        Guid policyVersionId) =>
        new(
            id,
            deviceId,
            borrowerId,
            borrowedAtUtc,
            dueAtUtc,
            policyVersionId,
            null,
            null,
            null,
            null);

    public LoanStatus GetStatus(DateTimeOffset effectiveNow)
    {
        if (!IsOpen)
        {
            return LoanStatus.Returned;
        }

        return DomainGuard.Utc(effectiveNow) >= DueAtUtc
            ? LoanStatus.Overdue
            : LoanStatus.Active;
    }

    public Loan Close(
        DateTimeOffset returnedAtUtc,
        Guid returnedByUserId,
        ReturnKind returnKind,
        Reason? returnReason)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("A returned loan cannot be closed again.");
        }

        return new Loan(
            Id,
            DeviceId,
            BorrowerId,
            BorrowedAtUtc,
            DueAtUtc,
            PolicyVersionId,
            returnedAtUtc,
            returnedByUserId,
            returnKind,
            returnReason);
    }

    public Loan ExtendDueAt(DateTimeOffset newDueAtUtc)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("A returned loan cannot be extended.");
        }

        var normalizedNewDueAt = DomainGuard.Utc(newDueAtUtc);
        if (normalizedNewDueAt <= DueAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newDueAtUtc),
                newDueAtUtc,
                "An extension must move the due time later.");
        }

        return new Loan(
            Id,
            DeviceId,
            BorrowerId,
            BorrowedAtUtc,
            normalizedNewDueAt,
            PolicyVersionId,
            null,
            null,
            null,
            null);
    }

    private void ValidateReturnTuple(
        DateTimeOffset? returnedAtUtc,
        Guid? returnedByUserId,
        ReturnKind? returnKind,
        Reason? returnReason)
    {
        var isEntireTupleEmpty =
            returnedAtUtc is null && returnedByUserId is null && returnKind is null && returnReason is null;
        if (isEntireTupleEmpty)
        {
            return;
        }

        if (returnedAtUtc is null || returnedByUserId is null || returnKind is null)
        {
            throw new ArgumentException("Return time, actor, and kind must be supplied together.");
        }

        var normalizedReturnedAt = DomainGuard.Utc(returnedAtUtc.Value);
        if (normalizedReturnedAt < BorrowedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(returnedAtUtc), returnedAtUtc, "Return time cannot precede borrow time.");
        }

        DomainGuard.RequiredId(returnedByUserId.Value, nameof(returnedByUserId));
        DomainGuard.DefinedEnum(returnKind.Value, nameof(returnKind));

        if (returnKind == Lending.ReturnKind.Self)
        {
            if (returnedByUserId != BorrowerId)
            {
                throw new ArgumentException("A self return must be performed by the borrower.", nameof(returnedByUserId));
            }

            if (returnReason is not null)
            {
                throw new ArgumentException("A self return cannot carry an administrator reason.", nameof(returnReason));
            }

            return;
        }

        if (returnedByUserId == BorrowerId)
        {
            throw new ArgumentException("A borrower must use the self-return path.", nameof(returnedByUserId));
        }

        if (returnReason is null)
        {
            throw new ArgumentException("An administrator-forced return requires a reason.", nameof(returnReason));
        }
    }
}
