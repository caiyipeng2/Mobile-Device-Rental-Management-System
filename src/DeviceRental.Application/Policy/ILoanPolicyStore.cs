using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;

namespace DeviceRental.Application.Policy;

public interface ILoanPolicyStore
{
    Task<LoanPolicyVersion?> GetCurrentAsync(
        DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default);

    Task<LoanPolicyVersion> CreateAsync(
        DurationMinutes duration,
        Guid changedByUserId,
        Reason reason,
        DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default);
}
