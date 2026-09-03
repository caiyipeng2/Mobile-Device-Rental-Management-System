using DeviceRental.Application.Policy;
using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace DeviceRental.Infrastructure.Devices;

public sealed class EfLoanPolicyStore(DeviceRentalDbContext dbContext) : ILoanPolicyStore
{
    public async Task<LoanPolicyVersion?> GetCurrentAsync(
        DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var record = await dbContext.Set<LoanPolicyVersionRecord>()
            .AsNoTracking()
            .Where(policy => policy.EffectiveAtUtc <= effectiveAtUtc.ToUniversalTime())
            .OrderByDescending(policy => policy.EffectiveAtUtc)
            .ThenByDescending(policy => policy.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public async Task<LoanPolicyVersion> CreateAsync(
        DurationMinutes duration,
        Guid changedByUserId,
        Reason reason,
        DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(duration);
        ArgumentNullException.ThrowIfNull(reason);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedEffectiveAt = effectiveAtUtc.ToUniversalTime();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var nextVersion = (await dbContext.Set<LoanPolicyVersionRecord>()
                .OrderByDescending(policy => policy.VersionNumber)
                .Select(policy => (int?)policy.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken) ?? 0) + 1;
            var policy = new LoanPolicyVersion(
                Guid.NewGuid(),
                nextVersion,
                duration,
                normalizedEffectiveAt,
                changedByUserId,
                reason);
            dbContext.Set<LoanPolicyVersionRecord>().Add(new LoanPolicyVersionRecord
            {
                Id = policy.Id,
                VersionNumber = policy.VersionNumber,
                DurationMinutes = policy.Duration.Value,
                EffectiveAtUtc = policy.EffectiveAtUtc,
                ChangedByUserId = policy.ChangedByUserId,
                Reason = policy.Reason.Value,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return policy;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static LoanPolicyVersion ToDomain(LoanPolicyVersionRecord record) =>
        new(
            record.Id,
            record.VersionNumber,
            DurationMinutes.From(record.DurationMinutes),
            record.EffectiveAtUtc,
            record.ChangedByUserId,
            Reason.From(record.Reason));
}
