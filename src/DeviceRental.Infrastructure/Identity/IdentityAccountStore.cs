using DeviceRental.Application.Identity;
using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DeviceRental.Infrastructure.Identity;

public sealed class IdentityAccountStore(
    DeviceRentalDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    INotificationOutboxWriter? notificationOutboxWriter = null,
    IConfiguration? configuration = null) : IAccountStore
{
    private static readonly string DummyPasswordHash = new PasswordHasher<ApplicationUser>()
        .HashPassword(new ApplicationUser { RealName = "dummy" }, "dummy password only used for timing parity");

    public async Task<AccountSnapshot?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        return user is null ? null : await ToSnapshotAsync(user);
    }

    public async Task<AccountCreationResult> CreateAsync(
        NewAccount account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await EnsureRoleExistsAsync(ToIdentityRole(account.Role), cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = account.Email,
            UserName = account.Email,
            RealName = account.RealName,
            IsActive = true,
            AuthorizationVersion = 1,
            EmailConfirmed = false,
            EmailVerifiedAt = null,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var create = await userManager.CreateAsync(user, account.Password);
        if (!create.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return create.Errors.Any(error =>
                error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))
                ? AccountCreationResult.DuplicateEmail()
                : AccountCreationResult.Failed();
        }

        var role = await userManager.AddToRoleAsync(user, ToIdentityRole(account.Role));
        if (!role.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AccountCreationResult.Failed();
        }

        await EnqueueVerificationNotificationAsync(user, user.CreatedAt, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return AccountCreationResult.Created(await ToSnapshotAsync(user));
    }

    public async Task<bool> VerifyPasswordAsync(
        Guid accountId,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(accountId.ToString());
        return user is not null && await userManager.CheckPasswordAsync(user, password);
    }

    public Task PerformDummyPasswordVerificationAsync(string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = new PasswordHasher<ApplicationUser>().VerifyHashedPassword(
            new ApplicationUser { RealName = "dummy" },
            DummyPasswordHash,
            password);
        return Task.CompletedTask;
    }

    public async Task<LoginFailureUpdate> RecordFailedSignInAsync(
        Guid accountId,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(accountId.ToString());
        if (user is null)
        {
            return LoginFailureUpdate.Recorded();
        }

        await userManager.AccessFailedAsync(user);
        return user.LockoutEnd is { } lockedUntilUtc && lockedUntilUtc > effectiveNowUtc.ToUniversalTime()
            ? LoginFailureUpdate.LockedUntil(lockedUntilUtc)
            : LoginFailureUpdate.Recorded();
    }

    public async Task ResetFailedSignInAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(accountId.ToString());
        if (user is not null)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }
    }

    public async Task<AccountToken?> GenerateEmailVerificationTokenAsync(
        string normalizedEmail,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || user.EmailConfirmed || !user.IsActive)
        {
            return null;
        }

        // Rotating the stamp makes a resent verification link invalidate any older link.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await userManager.UpdateSecurityStampAsync(user);
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var effectiveNow = effectiveNowUtc.ToUniversalTime();
            EnqueueTokenNotification(
                user,
                "ACCOUNT_EMAIL_VERIFICATION",
                "verification",
                "/Account/VerifyEmail",
                token,
                effectiveNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AccountToken(
                user.Id,
                user.Email ?? normalizedEmail,
                token,
                effectiveNow.AddHours(24));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<EmailVerificationResult> VerifyEmailAsync(
        string normalizedEmail,
        string token,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return EmailVerificationResult.InvalidToken();
        }

        if (user.EmailConfirmed)
        {
            return EmailVerificationResult.AlreadyVerified();
        }

        var verifiedAt = effectiveNowUtc.ToUniversalTime();
        user.EmailVerifiedAt = verifiedAt;
        user.UpdatedAt = verifiedAt;
        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? EmailVerificationResult.Verified()
            : EmailVerificationResult.InvalidToken();
    }

    public async Task<AccountToken?> GeneratePasswordResetTokenAsync(
        string normalizedEmail,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        // Only the most recently requested reset link remains valid.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await userManager.UpdateSecurityStampAsync(user);
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var effectiveNow = effectiveNowUtc.ToUniversalTime();
            EnqueueTokenNotification(
                user,
                "ACCOUNT_PASSWORD_RESET",
                "password-reset",
                "/Account/ResetPassword",
                token,
                effectiveNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AccountToken(
                user.Id,
                user.Email ?? normalizedEmail,
                token,
                effectiveNow.AddMinutes(30));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string normalizedEmail,
        string token,
        string newPassword,
        DateTimeOffset effectiveNowUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || !user.IsActive)
        {
            return PasswordResetResult.InvalidToken();
        }

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(error =>
                string.Equals(error.Code, nameof(IdentityErrorDescriber.InvalidToken), StringComparison.Ordinal))
                ? PasswordResetResult.InvalidToken()
                : PasswordResetResult.ValidationFailed(
                    [new FieldValidationError("password", "PASSWORD_POLICY_INVALID")]);
        }

        user.AuthorizationVersion++;
        user.UpdatedAt = effectiveNowUtc.ToUniversalTime();
        var update = await userManager.UpdateAsync(user);
        return update.Succeeded
            ? PasswordResetResult.Reset()
            : PasswordResetResult.InvalidToken();
    }

    private async Task<AccountSnapshot> ToSnapshotAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AccountSnapshot(
            user.Id,
            user.Email ?? throw new InvalidOperationException("Identity user email is required."),
            user.RealName,
            roles.Contains("TEST_ADMIN", StringComparer.Ordinal) ? AccountRole.TestAdmin : AccountRole.User,
            user.EmailConfirmed,
            user.IsActive,
            user.AuthorizationVersion,
            user.LockoutEnd);
    }

    private async Task EnqueueVerificationNotificationAsync(
        ApplicationUser user,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (notificationOutboxWriter is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        EnqueueTokenNotification(
            user,
            "ACCOUNT_EMAIL_VERIFICATION",
            "verification",
            "/Account/VerifyEmail",
            token,
            createdAtUtc,
            $"account:{user.Id:D}:verification",
            $"account-registration:{user.Id:D}");
    }

    private void EnqueueTokenNotification(
        ApplicationUser user,
        string eventType,
        string dedupeLabel,
        string path,
        string token,
        DateTimeOffset createdAtUtc,
        string? deduplicationKey = null,
        string? correlationId = null)
    {
        if (notificationOutboxWriter is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var urlPath = $"{path}?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";
        var baseUrl = configuration?["Notification:PublicBaseUrl"]?.TrimEnd('/');
        var url = string.IsNullOrWhiteSpace(baseUrl) ? urlPath : baseUrl + urlPath;
        notificationOutboxWriter.Enqueue(new NotificationOutboxRequest(
            deduplicationKey ?? $"account:{user.Id:D}:{dedupeLabel}:resend:{Guid.NewGuid():N}",
            eventType,
            "USER",
            user.Id.ToString("D"),
            user.AuthorizationVersion,
            correlationId ?? $"account:{user.Id:D}:resend",
            new NotificationPayload(
                user.Email,
                user.RealName,
                new Dictionary<string, string?>
                {
                    [eventType == "ACCOUNT_PASSWORD_RESET" ? "resetUrl" : "verificationUrl"] = url,
                },
                user.Id),
            createdAtUtc));
    }

    private static string ToIdentityRole(AccountRole role) => role switch
    {
        AccountRole.User => "USER",
        AccountRole.TestAdmin => "TEST_ADMIN",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown account role."),
    };

    private async Task EnsureRoleExistsAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var create = await roleManager.CreateAsync(new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            NormalizedName = roleName,
        });
        if (!create.Succeeded && !await roleManager.RoleExistsAsync(roleName))
        {
            throw new InvalidOperationException(
                $"The required application role '{roleName}' could not be created.");
        }
    }
}
