using DeviceRental.Application.Notifications;
using DeviceRental.Application.Identity;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Options;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Xunit;

namespace DeviceRental.IntegrationTests.Notifications;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class IdentityAccountNotificationTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public async Task CreateAsync_commits_a_registration_verification_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        await using var provider = CreateIdentityProvider();
        await using var scope = provider.CreateAsyncScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<DeviceRentalDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var options = Options.Create(new NotificationEncryptionOptions
        {
            CurrentKeyVersion = "test-v1",
            CurrentKeyBase64 = key,
        });
        var writer = new EfNotificationOutboxWriter(
            scopedContext,
            new AesGcmNotificationPayloadCodec(options),
            options);
        var store = new IdentityAccountStore(scopedContext, userManager, roleManager, writer);

        var result = await store.CreateAsync(
            new NewAccount(
                "registration@example.internal",
                "Registration User",
                "Password-1234",
                AccountRole.User),
            cancellationToken);

        Assert.Equal(AccountCreationOutcome.Created, result.Outcome);
        await using var verify = CreateContext();
        var message = await verify.OutboxMessages.SingleOrDefaultAsync(
            value => value.EventType == "ACCOUNT_EMAIL_VERIFICATION",
            cancellationToken);
        Assert.NotNull(message);
        Assert.Equal("PENDING", message.Status);
        Assert.StartsWith("account:", message.DedupeKey, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public async Task GenerateEmailVerificationTokenAsync_commits_a_resend_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        await using var provider = CreateIdentityProvider();
        await using var scope = provider.CreateAsyncScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<DeviceRentalDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "resend@example.internal",
            UserName = "resend@example.internal",
            RealName = "Resend User",
            IsActive = true,
            AuthorizationVersion = 1,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        Assert.True((await userManager.CreateAsync(user, "Password-1234")).Succeeded);
        var found = await userManager.FindByEmailAsync(user.Email);
        Assert.NotNull(found);
        Assert.False(found!.EmailConfirmed);
        Assert.True(found.IsActive);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var options = Options.Create(new NotificationEncryptionOptions
        {
            CurrentKeyVersion = "test-v1",
            CurrentKeyBase64 = key,
        });
        var writer = new EfNotificationOutboxWriter(
            scopedContext,
            new AesGcmNotificationPayloadCodec(options),
            options);
        var store = new IdentityAccountStore(scopedContext, userManager, roleManager, writer);
        var effectiveNow = DateTimeOffset.Parse("2026-09-04T12:00:00Z");

        var token = await store.GenerateEmailVerificationTokenAsync(
            user.Email,
            effectiveNow,
            cancellationToken);

        Assert.NotNull(token);
        await using var verify = CreateContext();
        var message = await verify.OutboxMessages.SingleOrDefaultAsync(
            value => value.EventType == "ACCOUNT_EMAIL_VERIFICATION",
            cancellationToken);
        Assert.NotNull(message);
        Assert.StartsWith($"account:{user.Id:D}:verification:resend:", message!.DedupeKey, StringComparison.Ordinal);
        Assert.Equal("PENDING", message.Status);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public async Task GeneratePasswordResetTokenAsync_commits_a_resend_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        await using var provider = CreateIdentityProvider();
        await using var scope = provider.CreateAsyncScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<DeviceRentalDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "reset@example.internal",
            UserName = "reset@example.internal",
            RealName = "Reset User",
            IsActive = true,
            AuthorizationVersion = 1,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        Assert.True((await userManager.CreateAsync(user, "Password-1234")).Succeeded);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var options = Options.Create(new NotificationEncryptionOptions
        {
            CurrentKeyVersion = "test-v1",
            CurrentKeyBase64 = key,
        });
        var writer = new EfNotificationOutboxWriter(
            scopedContext,
            new AesGcmNotificationPayloadCodec(options),
            options);
        var store = new IdentityAccountStore(scopedContext, userManager, roleManager, writer);
        var effectiveNow = DateTimeOffset.Parse("2026-09-04T12:00:00Z");

        var token = await store.GeneratePasswordResetTokenAsync(
            user.Email,
            effectiveNow,
            cancellationToken);

        Assert.NotNull(token);
        await using var verify = CreateContext();
        var message = await verify.OutboxMessages.SingleOrDefaultAsync(
            value => value.EventType == "ACCOUNT_PASSWORD_RESET",
            cancellationToken);
        Assert.NotNull(message);
        Assert.StartsWith($"account:{user.Id:D}:password-reset:resend:", message!.DedupeKey, StringComparison.Ordinal);
        Assert.Equal("PENDING", message.Status);
    }

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);

    private ServiceProvider CreateIdentityProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DeviceRentalDbContext>(options =>
            options.UseNpgsql(database.MigrationConnectionString, postgres =>
            {
                postgres.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            }));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Tokens.EmailConfirmationTokenProvider = "Test";
                options.Tokens.PasswordResetTokenProvider = "Test";
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<DeviceRentalDbContext>()
            .AddTokenProvider<TestTokenProvider>("Test");
        return services.BuildServiceProvider();
    }

    private sealed class TestTokenProvider : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public Task<bool> CanGenerateTwoFactorTokenAsync(
            UserManager<ApplicationUser> manager,
            ApplicationUser user) =>
            Task.FromResult(true);

        public Task<string> GenerateAsync(
            string purpose,
            UserManager<ApplicationUser> manager,
            ApplicationUser user) =>
            Task.FromResult("test-token");

        public Task<bool> ValidateAsync(
            string purpose,
            string token,
            UserManager<ApplicationUser> manager,
            ApplicationUser user) =>
            Task.FromResult(token == "test-token");
    }
}
