namespace DeviceRental.Application.Identity;

public sealed class AccountApplicationService(
    CorporateEmailPolicy corporateEmailPolicy,
    IAccountStore accountStore) : IAccountApplicationService
{
    private const int MinimumPasswordLength = 12;
    private const int MaximumPasswordLength = 128;
    private const int MaximumRealNameLength = 200;

    public async Task<RegistrationResult> RegisterAsync(
        RegistrationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = ValidateRegistration(input, out var email, out var realName);
        if (errors.Count != 0)
        {
            return RegistrationResult.ValidationFailed(errors);
        }

        var created = await accountStore.CreateAsync(
            new NewAccount(email!, realName!, input.Password!, AccountRole.User),
            cancellationToken);

        return created.Outcome switch
        {
            AccountCreationOutcome.Created when created.Account is not null => RegistrationResult.Created(created.Account),
            AccountCreationOutcome.DuplicateEmail => RegistrationResult.DuplicateEmail(),
            _ => RegistrationResult.Failed(),
        };
    }

    public async Task<SignInResult> SignInAsync(
        string? email,
        string? password,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        var safePassword = password ?? string.Empty;
        var emailDecision = corporateEmailPolicy.Evaluate(email);
        if (!emailDecision.IsAllowed || emailDecision.NormalizedEmail is null)
        {
            await accountStore.PerformDummyPasswordVerificationAsync(safePassword, cancellationToken);
            return SignInResult.InvalidCredentials();
        }

        var account = await accountStore.FindByNormalizedEmailAsync(
            emailDecision.NormalizedEmail,
            cancellationToken);
        if (account is null)
        {
            await accountStore.PerformDummyPasswordVerificationAsync(safePassword, cancellationToken);
            return SignInResult.InvalidCredentials();
        }

        var now = effectiveNowUtc.ToUniversalTime();
        if (account.LockedUntilUtc is { } lockedUntilUtc && lockedUntilUtc > now)
        {
            return SignInResult.Locked(lockedUntilUtc);
        }

        var passwordMatches = await accountStore.VerifyPasswordAsync(account.Id, safePassword, cancellationToken);
        if (!passwordMatches)
        {
            var failureUpdate = await accountStore.RecordFailedSignInAsync(account.Id, now, cancellationToken);
            return failureUpdate.IsLocked
                ? SignInResult.Locked(failureUpdate.LockedUntilUtc!.Value)
                : SignInResult.InvalidCredentials();
        }

        // A verified password alone must not activate an unverified or disabled account.
        if (!account.IsEmailVerified || !account.IsActive)
        {
            return SignInResult.InvalidCredentials();
        }

        await accountStore.ResetFailedSignInAsync(account.Id, cancellationToken);
        return SignInResult.Authenticated(account);
    }

    public async Task<EmailVerificationRequestResult> RequestEmailVerificationAsync(
        string? email,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeAllowedEmail(email);
        if (normalizedEmail is null)
        {
            return EmailVerificationRequestResult.Accepted(null);
        }

        var token = await accountStore.GenerateEmailVerificationTokenAsync(
            normalizedEmail,
            effectiveNowUtc.ToUniversalTime(),
            cancellationToken);
        return EmailVerificationRequestResult.Accepted(token);
    }

    public async Task<EmailVerificationResult> VerifyEmailAsync(
        string? email,
        string? token,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeAllowedEmail(email);
        if (normalizedEmail is null || string.IsNullOrWhiteSpace(token))
        {
            return EmailVerificationResult.InvalidToken();
        }

        return await accountStore.VerifyEmailAsync(
            normalizedEmail,
            token,
            effectiveNowUtc.ToUniversalTime(),
            cancellationToken);
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(
        string? email,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        // The accepted result is intentionally identical for unknown and known accounts.
        var normalizedEmail = NormalizeAllowedEmail(email);
        var token = normalizedEmail is null
            ? null
            : await accountStore.GeneratePasswordResetTokenAsync(
                normalizedEmail,
                effectiveNowUtc.ToUniversalTime(),
                cancellationToken);
        return PasswordResetRequestResult.Accepted(token);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string? email,
        string? token,
        string? newPassword,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeAllowedEmail(email);
        var errors = ValidatePassword(newPassword);
        if (normalizedEmail is null || string.IsNullOrWhiteSpace(token))
        {
            return PasswordResetResult.InvalidToken();
        }

        if (errors.Count != 0)
        {
            return PasswordResetResult.ValidationFailed(errors);
        }

        return await accountStore.ResetPasswordAsync(
            normalizedEmail,
            token,
            newPassword!,
            effectiveNowUtc.ToUniversalTime(),
            cancellationToken);
    }

    private List<FieldValidationError> ValidateRegistration(
        RegistrationInput input,
        out string? normalizedEmail,
        out string? normalizedRealName)
    {
        normalizedEmail = null;
        normalizedRealName = null;
        var errors = new List<FieldValidationError>();

        var emailDecision = corporateEmailPolicy.Evaluate(input.Email);
        if (!emailDecision.IsAllowed || emailDecision.NormalizedEmail is null)
        {
            errors.Add(new FieldValidationError("email", "CORPORATE_EMAIL_REQUIRED"));
        }
        else
        {
            normalizedEmail = emailDecision.NormalizedEmail;
        }

        normalizedRealName = input.RealName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRealName) || normalizedRealName.Length > MaximumRealNameLength)
        {
            errors.Add(new FieldValidationError("realName", "REAL_NAME_REQUIRED"));
        }

        if (input.Password is null ||
            input.Password.Length < MinimumPasswordLength ||
            input.Password.Length > MaximumPasswordLength)
        {
            errors.Add(new FieldValidationError("password", "PASSWORD_LENGTH_INVALID"));
        }

        return errors;
    }

    private string? NormalizeAllowedEmail(string? email)
    {
        var decision = corporateEmailPolicy.Evaluate(email);
        return decision.IsAllowed ? decision.NormalizedEmail : null;
    }

    private static List<FieldValidationError> ValidatePassword(string? password) =>
        password is null || password.Length is < MinimumPasswordLength or > MaximumPasswordLength
            ? [new FieldValidationError("password", "PASSWORD_LENGTH_INVALID")]
            : [];
}
