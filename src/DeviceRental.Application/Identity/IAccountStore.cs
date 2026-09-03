namespace DeviceRental.Application.Identity;

/// <summary>
/// Owns credential persistence and atomic failed-sign-in state transitions.
/// Implementations must apply failure increments and lock decisions atomically for one account.
/// </summary>
public interface IAccountStore
{
    Task<AccountSnapshot?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<AccountCreationResult> CreateAsync(
        NewAccount account,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyPasswordAsync(
        Guid accountId,
        string password,
        CancellationToken cancellationToken = default);

    Task PerformDummyPasswordVerificationAsync(
        string password,
        CancellationToken cancellationToken = default);

    Task<LoginFailureUpdate> RecordFailedSignInAsync(
        Guid accountId,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task ResetFailedSignInAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<AccountToken?> GenerateEmailVerificationTokenAsync(
        string normalizedEmail,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<EmailVerificationResult> VerifyEmailAsync(
        string normalizedEmail,
        string token,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<AccountToken?> GeneratePasswordResetTokenAsync(
        string normalizedEmail,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(
        string normalizedEmail,
        string token,
        string newPassword,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record AccountToken(
    Guid AccountId,
    string Email,
    string Value,
    DateTimeOffset ExpiresAtUtc);

public enum AccountCreationOutcome
{
    Created,
    DuplicateEmail,
    Failed,
}

public sealed record AccountCreationResult(AccountCreationOutcome Outcome, AccountSnapshot? Account)
{
    public static AccountCreationResult Created(AccountSnapshot account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new(AccountCreationOutcome.Created, account);
    }

    public static AccountCreationResult DuplicateEmail() => new(AccountCreationOutcome.DuplicateEmail, null);

    public static AccountCreationResult Failed() => new(AccountCreationOutcome.Failed, null);
}

public sealed record LoginFailureUpdate(DateTimeOffset? LockedUntilUtc)
{
    public bool IsLocked => LockedUntilUtc is not null;

    public static LoginFailureUpdate Recorded() => new((DateTimeOffset?)null);

    public static LoginFailureUpdate LockedUntil(DateTimeOffset lockedUntilUtc) =>
        new(lockedUntilUtc.ToUniversalTime());
}
