namespace DeviceRental.Application.Identity;

public interface IAccountApplicationService
{
    Task<RegistrationResult> RegisterAsync(
        RegistrationInput input,
        CancellationToken cancellationToken = default);

    Task<SignInResult> SignInAsync(
        string? email,
        string? password,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record RegistrationInput(string? Email, string? RealName, string? Password);

public sealed record FieldValidationError(string Field, string Code);

public enum RegistrationOutcome
{
    CreatedPendingEmailVerification,
    ValidationFailed,
    DuplicateEmail,
    Failed,
}

public sealed record RegistrationResult(
    RegistrationOutcome Outcome,
    AccountSnapshot? Account,
    IReadOnlyList<FieldValidationError> Errors)
{
    public static RegistrationResult Created(AccountSnapshot account) =>
        new(RegistrationOutcome.CreatedPendingEmailVerification, account, []);

    public static RegistrationResult ValidationFailed(IReadOnlyList<FieldValidationError> errors) =>
        new(RegistrationOutcome.ValidationFailed, null, errors);

    public static RegistrationResult DuplicateEmail() =>
        new(RegistrationOutcome.DuplicateEmail, null, []);

    public static RegistrationResult Failed() => new(RegistrationOutcome.Failed, null, []);
}

public enum SignInOutcome
{
    Authenticated,
    InvalidCredentials,
    Locked,
}

public sealed record SignInResult(
    SignInOutcome Outcome,
    AccountSnapshot? Account,
    DateTimeOffset? LockedUntilUtc)
{
    public static SignInResult Authenticated(AccountSnapshot account) =>
        new(SignInOutcome.Authenticated, account, null);

    public static SignInResult InvalidCredentials() =>
        new(SignInOutcome.InvalidCredentials, null, null);

    public static SignInResult Locked(DateTimeOffset lockedUntilUtc) =>
        new(SignInOutcome.Locked, null, lockedUntilUtc.ToUniversalTime());
}
