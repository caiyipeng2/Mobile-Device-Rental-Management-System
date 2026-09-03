using DeviceRental.Domain.Common;
using DeviceRental.Domain.Lending;
using DeviceRental.Infrastructure.Devices;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeviceRental.IntegrationTests.Devices;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class EfLoanPolicyStoreTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task Create_then_get_current_round_trips_the_policy_version()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var actor = await CreateUserAsync(cancellationToken);
        var effectiveAt = DateTimeOffset.UtcNow;

        await using (var context = CreateContext())
        {
            var store = new EfLoanPolicyStore(context);
            var created = await store.CreateAsync(
                DurationMinutes.From(180),
                actor.Id,
                Reason.From("专项回归测试"),
                effectiveAt,
                cancellationToken);

            Assert.Equal(1, created.VersionNumber);
            Assert.Equal(180, created.Duration.Value);
        }

        await using var readContext = CreateContext();
        var current = await new EfLoanPolicyStore(readContext)
            .GetCurrentAsync(effectiveAt.AddMinutes(1), cancellationToken);

        Assert.NotNull(current);
        Assert.Equal(180, current!.Duration.Value);
        Assert.Equal(actor.Id, current.ChangedByUserId);
        Assert.Equal("专项回归测试", current.Reason.Value);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Get_current_ignores_a_future_policy_version()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var actor = await CreateUserAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        var store = new EfLoanPolicyStore(context);
        await store.CreateAsync(
            DurationMinutes.From(240),
            actor.Id,
            Reason.From("明日生效"),
            now.AddHours(1),
            cancellationToken);

        var current = await store.GetCurrentAsync(now, cancellationToken);

        Assert.Null(current);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task Get_current_returns_the_latest_effective_version()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var actor = await CreateUserAsync(cancellationToken);
        var firstEffectiveAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var secondEffectiveAt = firstEffectiveAt.AddMinutes(1);

        await using var context = CreateContext();
        var store = new EfLoanPolicyStore(context);
        await store.CreateAsync(
            DurationMinutes.From(180),
            actor.Id,
            Reason.From("首版借期"),
            firstEffectiveAt,
            cancellationToken);
        var second = await store.CreateAsync(
            DurationMinutes.From(300),
            actor.Id,
            Reason.From("回归窗口调整"),
            secondEffectiveAt,
            cancellationToken);

        var current = await store.GetCurrentAsync(secondEffectiveAt.AddSeconds(1), cancellationToken);

        Assert.NotNull(current);
        Assert.Equal(second.Id, current!.Id);
        Assert.Equal(300, current.Duration.Value);
        Assert.Equal(2, current.VersionNumber);
    }

    private async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);

    private async Task<ApplicationUser> CreateUserAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var email = $"policy-{Guid.NewGuid():N}@example.internal";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            RealName = "Policy Admin",
            IsActive = true,
            AuthorizationVersion = 1,
            LockoutEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await using var context = CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }
}
