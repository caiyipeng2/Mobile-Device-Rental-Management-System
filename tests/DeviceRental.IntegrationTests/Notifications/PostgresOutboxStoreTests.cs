using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Records;
using DeviceRental.Domain.Notifications;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeviceRental.IntegrationTests.Notifications;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class PostgresOutboxStoreTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task ClaimDueAsync_ClaimsEachDueMessageOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await SeedMessagesAsync(now, cancellationToken);

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstStore = new PostgresOutboxStore(firstContext);
        var secondStore = new PostgresOutboxStore(secondContext);

        var results = await Task.WhenAll(
            firstStore.ClaimDueAsync(now, "worker-a", 2, TimeSpan.FromMinutes(5), cancellationToken),
            secondStore.ClaimDueAsync(now, "worker-b", 2, TimeSpan.FromMinutes(5), cancellationToken));

        var claims = results.SelectMany(value => value).ToArray();
        Assert.Equal(2, claims.Length);
        Assert.Equal(2, claims.Select(value => value.EventId).Distinct().Count());
        Assert.Equal(2, claims.Select(value => value.LeaseId).Distinct().Count());

        await using var verify = CreateContext();
        Assert.Equal(2, await verify.OutboxMessages.CountAsync(value => value.Status == "CLAIMED", cancellationToken));
        Assert.Equal(1, await verify.OutboxMessages.CountAsync(value => value.Status == "PENDING", cancellationToken));
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-008")]
    public async Task TryStartSendingAsync_RejectsAnExpiredLease()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await SeedMessagesAsync(now, cancellationToken);

        await using var context = CreateContext();
        var store = new PostgresOutboxStore(context);
        var claim = Assert.Single(await store.ClaimDueAsync(
            now,
            "worker-a",
            1,
            TimeSpan.FromMinutes(1),
            cancellationToken));

        var started = await store.TryStartSendingAsync(
            claim.EventId,
            claim.LeaseId,
            now.AddMinutes(2),
            cancellationToken);

        Assert.False(started);
        await using var verify = CreateContext();
        var record = await verify.OutboxMessages.SingleAsync(value => value.EventId == claim.EventId, cancellationToken);
        Assert.Equal("CLAIMED", record.Status);
        Assert.Null(record.SendingStartedAt);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public async Task TryStartSendingAsync_AdvancesOnlyTheCurrentLease()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await SeedMessagesAsync(now, cancellationToken);

        await using var context = CreateContext();
        var store = new PostgresOutboxStore(context);
        var claim = Assert.Single(await store.ClaimDueAsync(
            now,
            "worker-a",
            1,
            TimeSpan.FromMinutes(5),
            cancellationToken));

        Assert.True(await store.TryStartSendingAsync(claim.EventId, claim.LeaseId, now.AddMinutes(1), cancellationToken));
        Assert.False(await store.TryStartSendingAsync(claim.EventId, Guid.NewGuid(), now.AddMinutes(1), cancellationToken));

        await using var verify = CreateContext();
        var record = await verify.OutboxMessages.SingleAsync(value => value.EventId == claim.EventId, cancellationToken);
        Assert.Equal("SENDING", record.Status);
        Assert.Equal(1, record.Attempts);
        Assert.NotNull(record.SendingStartedAt);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public async Task NotificationDelivery_DedupeKey_is_unique_per_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage("dedupe", now.AddMinutes(-1), now.AddMinutes(-1));
        await using (var seed = CreateContext())
        {
            seed.OutboxMessages.Add(message);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = CreateContext();
        context.NotificationDeliveries.Add(CreateDelivery(message.EventId, "delivery:1"));
        await context.SaveChangesAsync(cancellationToken);
        context.NotificationDeliveries.Add(CreateDelivery(message.EventId, "delivery:1"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public async Task RecordDeliveryAsync_Persists_the_attempt_for_the_current_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        await SeedMessagesAsync(now, cancellationToken);

        await using var context = CreateContext();
        var store = new PostgresOutboxStore(context);
        var claim = Assert.Single(await store.ClaimDueAsync(
            now,
            "worker-a",
            1,
            TimeSpan.FromMinutes(5),
            cancellationToken));
        Assert.True(await store.TryStartSendingAsync(claim.EventId, claim.LeaseId, now.AddMinutes(1), cancellationToken));

        var recipient = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "delivery@example.internal",
            NormalizedUserName = "DELIVERY@EXAMPLE.INTERNAL",
            Email = "delivery@example.internal",
            NormalizedEmail = "DELIVERY@EXAMPLE.INTERNAL",
            RealName = "Delivery User",
            IsActive = true,
            AuthorizationVersion = 1,
            LockoutEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        context.Users.Add(recipient);
        await context.SaveChangesAsync(cancellationToken);

        var result = NotificationSendResult.Accepted("smtp:250").WithDeliveryMetadata(
            "loan-borrowed-v1",
            recipient.Id);
        Assert.True(await store.RecordDeliveryAsync(claim, result, now.AddMinutes(1), now.AddMinutes(1), cancellationToken));

        await using var verify = CreateContext();
        var delivery = await verify.NotificationDeliveries.SingleAsync(cancellationToken);
        Assert.Equal(claim.EventId, delivery.EventId);
        Assert.Equal($"{claim.DeduplicationKey}:attempt:1", delivery.DedupeKey);
        Assert.Equal("ACCEPTED", delivery.Outcome);
        Assert.Equal("smtp:250", delivery.AcceptanceEvidenceReference);
    }

    private async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private async Task SeedMessagesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var context = CreateContext();
        context.OutboxMessages.AddRange(
            CreateMessage("due-1", now.AddMinutes(-2), now.AddMinutes(-1)),
            CreateMessage("due-2", now.AddMinutes(-3), now.AddMinutes(-1)),
            CreateMessage("future", now.AddMinutes(-1), now.AddMinutes(5)));
        await context.SaveChangesAsync(cancellationToken);
    }

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);

    private static OutboxMessageRecord CreateMessage(string suffix, DateTimeOffset createdAt, DateTimeOffset availableAt) =>
        new()
        {
            EventId = Guid.NewGuid(),
            DedupeKey = $"notification:{suffix}",
            EventType = "LOAN_DUE",
            AggregateType = "LOAN",
            AggregateId = Guid.NewGuid().ToString("D"),
            AggregateVersion = 1,
            CorrelationId = $"correlation:{suffix}",
            PayloadSchemaVersion = 1,
            PayloadKeyVersion = "test-key-v1",
            PayloadCiphertext = [1, 2, 3],
            CreatedAt = createdAt,
            AvailableAt = availableAt,
            Status = "PENDING",
            Attempts = 0,
        };

    private static NotificationDeliveryRecord CreateDelivery(Guid eventId, string dedupeKey) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            DedupeKey = dedupeKey,
            RecipientKeyVersion = "recipient-key-v1",
            RecipientCiphertext = [4, 5, 6],
            Channel = "EMAIL",
            TemplateIdentifier = "loan-due-v1",
            AttemptNumber = 1,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Outcome = "ACCEPTED",
            AcceptanceEvidence = "ACCEPTED",
            AcceptanceEvidenceReference = "smtp:accepted",
        };
}
