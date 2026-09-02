using DeviceRental.Application.Identity;
using DeviceRental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeviceRental.Infrastructure.Identity;

public sealed class IdentityAccountStore(
    DeviceRentalDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager) : IAccountStore
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
