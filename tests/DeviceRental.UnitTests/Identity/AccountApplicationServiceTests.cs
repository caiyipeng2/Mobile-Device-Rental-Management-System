using DeviceRental.Application.Identity;
using Xunit;

namespace DeviceRental.UnitTests.Identity;

public sealed class AccountApplicationServiceTests
{
    private readonly CorporateEmailPolicy _corporateEmailPolicy = new(["example.com"]);

    [Fact]
    [Trait("Requirement", "REQ-AUTH-001")]
    [Trait("Requirement", "REQ-AUTH-002")]
    [Trait("Requirement", "REQ-AUTH-004")]
    [Trait("MvpCase", "AUTH-001")]
    public async Task RegisterAsync_CreatesAnUnverifiedStandardUserWithNormalizedInput()
    {
        var store = new FakeAccountStore();
        var service = new AccountApplicationService(_corporateEmailPolicy, store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.RegisterAsync(new RegistrationInput(
            " Alice@Example.COM ",
            "  Alice Zhang  ",
            "correct horse battery staple"), cancellationToken);

        Assert.Equal(RegistrationOutcome.CreatedPendingEmailVerification, result.Outcome);
        Assert.NotNull(result.Account);
        Assert.Equal("alice@example.com", result.Account.Email);
        Assert.Equal("Alice Zhang", result.Account.RealName);
        Assert.False(result.Account.IsEmailVerified);
        Assert.Equal(AccountRole.User, result.Account.Role);
        Assert.Equal("alice@example.com", store.CreatedAccount!.Email);
        Assert.Equal("Alice Zhang", store.CreatedAccount.RealName);
        Assert.Equal(AccountRole.User, store.CreatedAccount.Role);
    }

    [Theory]
    [InlineData("user@outside.example")]
    [InlineData("not-an-email")]
    [Trait("Requirement", "REQ-AUTH-002")]
    public async Task RegisterAsync_RejectsAddressesOutsideTheConfiguredCorporateDomain(string email)
    {
        var store = new FakeAccountStore();
        var service = new AccountApplicationService(_corporateEmailPolicy, store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.RegisterAsync(
            new RegistrationInput(email, "Alice", "correct horse battery staple"),
            cancellationToken);

        Assert.Equal(RegistrationOutcome.ValidationFailed, result.Outcome);
        Assert.Contains(result.Errors, error => error.Field == "email");
        Assert.Null(store.CreatedAccount);
    }

    [Theory]
    [InlineData("", "correct horse battery staple", "realName")]
    [InlineData("Alice", "short", "password")]
    [InlineData("Alice", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "password")]
    [Trait("Requirement", "REQ-AUTH-001")]
    [Trait("Requirement", "REQ-AUTH-006")]
    public async Task RegisterAsync_RejectsBlankRealNameAndPasswordsOutsideApprovedBounds(
        string realName,
        string password,
        string expectedField)
    {
        var store = new FakeAccountStore();
        var service = new AccountApplicationService(_corporateEmailPolicy, store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.RegisterAsync(
            new RegistrationInput("alice@example.com", realName, password),
            cancellationToken);

        Assert.Equal(RegistrationOutcome.ValidationFailed, result.Outcome);
        Assert.Contains(result.Errors, error => error.Field == expectedField);
        Assert.Null(store.CreatedAccount);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-006")]
    [Trait("MvpCase", "AUTH-006")]
    public async Task SignInAsync_ReturnsLockedWhenTheAtomicFailureUpdateLocksTheAccount()
    {
        var account = AccountSnapshot.PendingVerification(
            Guid.NewGuid(),
            "alice@example.com",
            "Alice",
            AccountRole.User) with { IsEmailVerified = true };
        var store = new FakeAccountStore
        {
            Account = account,
            PasswordMatches = false,
            FailureResult = LoginFailureUpdate.LockedUntil(DateTimeOffset.Parse("2026-09-01T10:15:00Z")),
        };
        var service = new AccountApplicationService(_corporateEmailPolicy, store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.SignInAsync(
            "alice@example.com",
            "incorrect password",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            cancellationToken);

        Assert.Equal(SignInOutcome.Locked, result.Outcome);
        Assert.Equal(DateTimeOffset.Parse("2026-09-01T10:15:00Z"), result.LockedUntilUtc);
        Assert.Equal(account.Id, store.FailureRecordedFor);
        Assert.False(store.FailureStateWasReset);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-003")]
    public async Task SignInAsync_DeniesAnUnverifiedAccountWithoutResettingItsFailureState()
    {
        var account = AccountSnapshot.PendingVerification(
            Guid.NewGuid(),
            "alice@example.com",
            "Alice",
            AccountRole.User);
        var store = new FakeAccountStore { Account = account, PasswordMatches = true };
        var service = new AccountApplicationService(_corporateEmailPolicy, store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.SignInAsync(
            "alice@example.com",
            "correct horse battery staple",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            cancellationToken);

        Assert.Equal(SignInOutcome.InvalidCredentials, result.Outcome);
        Assert.False(store.FailureStateWasReset);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-005")]
    public async Task SignInAsync_ResetsFailuresAndReturnsTheAccountAfterSuccessfulAuthentication()
    {
        var account = AccountSnapshot.PendingVerification(
            Guid.NewGuid(),
            "alice@example.com",
            "Alice",
            AccountRole.User) with { IsEmailVerified = true };
        var store = new FakeAccountStore { Account = account, PasswordMatches = true };
        var service = new AccountApplicationService(_corporateEmailPolicy, store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.SignInAsync(
            "alice@example.com",
            "correct horse battery staple",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            cancellationToken);

        Assert.Equal(SignInOutcome.Authenticated, result.Outcome);
        Assert.Equal(account, result.Account);
        Assert.True(store.FailureStateWasReset);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-003")]
    public async Task RequestEmailVerificationAsync_NormalizesEmailAndReturnsAcceptedToken()
    {
        var store = new FakeAccountStore
        {
            VerificationToken = new AccountToken(
                Guid.NewGuid(),
                "alice@example.com",
                "email-token",
                DateTimeOffset.Parse("2026-09-02T10:00:00Z")),
        };
        var service = new AccountApplicationService(_corporateEmailPolicy, store);

        var result = await service.RequestEmailVerificationAsync(
            " Alice@Example.COM ",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            TestContext.Current.CancellationToken);

        Assert.Equal(EmailVerificationRequestOutcome.Accepted, result.Outcome);
        Assert.Equal("alice@example.com", store.TokenRequestedFor);
        Assert.Equal("email-token", result.Token!.Value);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-003")]
    public async Task VerifyEmailAsync_RejectsBlankTokenWithoutTouchingTheStore()
    {
        var store = new FakeAccountStore();
        var service = new AccountApplicationService(_corporateEmailPolicy, store);

        var result = await service.VerifyEmailAsync(
            "alice@example.com",
            " ",
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(EmailVerificationOutcome.InvalidToken, result.Outcome);
        Assert.Null(store.VerifiedEmail);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-005")]
    public async Task RequestPasswordResetAsync_UsesGenericAcceptedResponseForUnknownAccount()
    {
        var store = new FakeAccountStore();
        var service = new AccountApplicationService(_corporateEmailPolicy, store);

        var result = await service.RequestPasswordResetAsync(
            "missing@example.com",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetRequestOutcome.Accepted, result.Outcome);
        Assert.Null(result.Token);
        Assert.Equal("missing@example.com", store.TokenRequestedFor);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-005")]
    public async Task ResetPasswordAsync_ValidatesPasswordBeforeConsumingToken()
    {
        var store = new FakeAccountStore();
        var service = new AccountApplicationService(_corporateEmailPolicy, store);

        var result = await service.ResetPasswordAsync(
            "alice@example.com",
            "reset-token",
            "short",
            DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetOutcome.ValidationFailed, result.Outcome);
        Assert.Contains(result.Errors, error => error.Field == "password");
        Assert.Null(store.ResetEmail);
    }

    [Fact]
    [Trait("Requirement", "REQ-AUTH-005")]
    public async Task ResetPasswordAsync_NormalizesEmailAndReturnsStoreOutcome()
    {
        var store = new FakeAccountStore
        {
            ResetResult = PasswordResetResult.Reset(),
        };
        var service = new AccountApplicationService(_corporateEmailPolicy, store);

        var result = await service.ResetPasswordAsync(
            " Alice@Example.COM ",
            "reset-token",
            "correct horse battery staple",
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetOutcome.Reset, result.Outcome);
        Assert.Equal("alice@example.com", store.ResetEmail);
        Assert.Equal("reset-token", store.ResetToken);
    }

    private sealed class FakeAccountStore : IAccountStore
    {
        public AccountSnapshot? Account { get; init; }

        public bool PasswordMatches { get; init; }

        public LoginFailureUpdate FailureResult { get; init; } = LoginFailureUpdate.Recorded();

        public NewAccount? CreatedAccount { get; private set; }

        public Guid? FailureRecordedFor { get; private set; }

        public bool FailureStateWasReset { get; private set; }

        public AccountToken? VerificationToken { get; init; }

        public AccountToken? PasswordResetToken { get; init; }

        public PasswordResetResult ResetResult { get; init; } = PasswordResetResult.InvalidToken();

        public string? TokenRequestedFor { get; private set; }

        public string? VerifiedEmail { get; private set; }

        public string? ResetEmail { get; private set; }

        public string? ResetToken { get; private set; }

        public Task<AccountSnapshot?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(Account);

        public Task<AccountCreationResult> CreateAsync(NewAccount account, CancellationToken cancellationToken = default)
        {
            CreatedAccount = account;
            return Task.FromResult(AccountCreationResult.Created(AccountSnapshot.PendingVerification(
                Guid.NewGuid(),
                account.Email,
                account.RealName,
                account.Role)));
        }

        public Task<bool> VerifyPasswordAsync(Guid accountId, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(PasswordMatches);

        public Task PerformDummyPasswordVerificationAsync(string password, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<LoginFailureUpdate> RecordFailedSignInAsync(
            Guid accountId,
            DateTimeOffset effectiveNowUtc,
            CancellationToken cancellationToken = default)
        {
            FailureRecordedFor = accountId;
            return Task.FromResult(FailureResult);
        }

        public Task ResetFailedSignInAsync(Guid accountId, CancellationToken cancellationToken = default)
        {
            FailureStateWasReset = true;
            return Task.CompletedTask;
        }

        public Task<AccountToken?> GenerateEmailVerificationTokenAsync(
            string normalizedEmail,
            DateTimeOffset effectiveNowUtc,
            CancellationToken cancellationToken = default)
        {
            TokenRequestedFor = normalizedEmail;
            return Task.FromResult(VerificationToken);
        }

        public Task<EmailVerificationResult> VerifyEmailAsync(
            string normalizedEmail,
            string token,
            DateTimeOffset effectiveNowUtc,
            CancellationToken cancellationToken = default)
        {
            VerifiedEmail = normalizedEmail;
            return Task.FromResult(EmailVerificationResult.Verified());
        }

        public Task<AccountToken?> GeneratePasswordResetTokenAsync(
            string normalizedEmail,
            DateTimeOffset effectiveNowUtc,
            CancellationToken cancellationToken = default)
        {
            TokenRequestedFor = normalizedEmail;
            return Task.FromResult(PasswordResetToken);
        }

        public Task<PasswordResetResult> ResetPasswordAsync(
            string normalizedEmail,
            string token,
            string newPassword,
            DateTimeOffset effectiveNowUtc,
            CancellationToken cancellationToken = default)
        {
            ResetEmail = normalizedEmail;
            ResetToken = token;
            return Task.FromResult(ResetResult);
        }
    }
}
