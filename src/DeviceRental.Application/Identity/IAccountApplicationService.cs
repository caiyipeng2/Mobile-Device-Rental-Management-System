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

    Task<EmailVerificationRequestResult> RequestEmailVerificationAsync(
        string? email,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<EmailVerificationResult> VerifyEmailAsync(
        string? email,
        string? token,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<PasswordResetRequestResult> RequestPasswordResetAsync(
        string? email,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);

    Task<PasswordResetResult> ResetPasswordAsync(
        string? email,
        string? token,
        string? newPassword,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record RegistrationInput(string? Email, string? RealName, string? Password);

public enum EmailVerificationRequestOutcome
{
    Accepted,
}

public sealed record EmailVerificationRequestResult(
    EmailVerificationRequestOutcome Outcome,
    AccountToken? Token)
{
    public static EmailVerificationRequestResult Accepted(AccountToken? token) =>
        new(EmailVerificationRequestOutcome.Accepted, token);
}

public enum EmailVerificationOutcome
{
    Verified,
    AlreadyVerified,
    InvalidToken,
}

public sealed record EmailVerificationResult(EmailVerificationOutcome Outcome)
{
    public static EmailVerificationResult Verified() => new(EmailVerificationOutcome.Verified);

    public static EmailVerificationResult AlreadyVerified() => new(EmailVerificationOutcome.AlreadyVerified);

    public static EmailVerificationResult InvalidToken() => new(EmailVerificationOutcome.InvalidToken);
}

public enum PasswordResetRequestOutcome
{
    Accepted,
}

public sealed record PasswordResetRequestResult(
    PasswordResetRequestOutcome Outcome,
    AccountToken? Token)
{
    public static PasswordResetRequestResult Accepted(AccountToken? token) =>
        new(PasswordResetRequestOutcome.Accepted, token);
}

public enum PasswordResetOutcome
{
    Reset,
    InvalidToken,
    ValidationFailed,
}

public sealed record PasswordResetResult(
    PasswordResetOutcome Outcome,
    IReadOnlyList<FieldValidationError> Errors)
{
    public static PasswordResetResult Reset() => new(PasswordResetOutcome.Reset, []);

    public static PasswordResetResult InvalidToken() => new(PasswordResetOutcome.InvalidToken, []);

    public static PasswordResetResult ValidationFailed(IReadOnlyList<FieldValidationError> errors) =>
        new(PasswordResetOutcome.ValidationFailed, errors);
}

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
