using System.Security.Cryptography;
using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Options;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Records;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeviceRental.IntegrationTests.Notifications;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class EfNotificationOutboxWriterTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-001")]
    public async Task Enqueue_adds_an_encrypted_outbox_event_to_the_current_transaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var codec = new AesGcmNotificationPayloadCodec(Options.Create(new NotificationEncryptionOptions
        {
            CurrentKeyVersion = "test-v1",
            CurrentKeyBase64 = key,
        }));
        var writer = new EfNotificationOutboxWriter(
            context,
            codec,
            Options.Create(new NotificationEncryptionOptions
            {
                CurrentKeyVersion = "test-v1",
                CurrentKeyBase64 = key,
            }));
        var eventId = Guid.NewGuid();

        writer.Enqueue(new NotificationOutboxRequest(
            $"account:{eventId:D}:verification",
            "ACCOUNT_EMAIL_VERIFICATION",
            "USER",
            eventId.ToString("D"),
            1,
            "correlation-account-1",
            new NotificationPayload(
                "alice@example.com",
                "Alice",
                new Dictionary<string, string?> { ["verificationUrl"] = "https://desk.test/verify" },
                eventId),
            DateTimeOffset.Parse("2026-09-04T10:00:00Z")));

        await context.SaveChangesAsync(cancellationToken);
        var persisted = await context.OutboxMessages.SingleAsync(cancellationToken);
        Assert.Equal("ACCOUNT_EMAIL_VERIFICATION", persisted.EventType);
        Assert.Equal("PENDING", persisted.Status);
        Assert.NotNull(persisted.PayloadCiphertext);
        Assert.NotEmpty(persisted.PayloadCiphertext!);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-008")]
    public async Task CancelPendingRemindersAsync_cancels_pending_and_claimed_reminders_only()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        var loanId = Guid.NewGuid().ToString("D");
        var createdAt = DateTimeOffset.Parse("2026-09-04T10:00:00Z");
        var claimedLease = Guid.NewGuid();
        context.OutboxMessages.AddRange(
            CreateReminder("advance", loanId, "PENDING", createdAt, null, null, null),
            CreateReminder("due", loanId, "CLAIMED", createdAt, claimedLease, "worker-a", createdAt.AddMinutes(5), "LOAN_DUE"),
            CreateReminder("borrowed", loanId, "PENDING", createdAt, null, null, null, "LOAN_BORROWED"));
        await context.SaveChangesAsync(cancellationToken);

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var writer = new EfNotificationOutboxWriter(
            context,
            new AesGcmNotificationPayloadCodec(Options.Create(new NotificationEncryptionOptions
            {
                CurrentKeyVersion = "test-v1",
                CurrentKeyBase64 = key,
            })),
            Options.Create(new NotificationEncryptionOptions
            {
                CurrentKeyVersion = "test-v1",
                CurrentKeyBase64 = key,
            }));
        var canceledAt = createdAt.AddMinutes(10);

        var canceled = await writer.CancelPendingRemindersAsync(
            "LOAN",
            loanId,
            canceledAt,
            cancellationToken);

        Assert.Equal(2, canceled);
        await using var verify = CreateContext();
        var rows = await verify.OutboxMessages
            .Where(value => value.AggregateId == loanId)
            .OrderBy(value => value.EventType)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, rows.Count(value => value.Status == "CANCELLED"));
        Assert.All(rows.Where(value => value.EventType is "LOAN_ADVANCE_REMINDER" or "LOAN_DUE"), value =>
        {
            Assert.Equal("CANCELLED", value.Status);
            Assert.Equal(canceledAt, value.CanceledAt);
            Assert.Null(value.LastError);
        });
        var claimed = Assert.Single(rows, value => value.EventType == "LOAN_DUE");
        Assert.Equal(claimedLease, claimed.LeaseId);
        Assert.Equal("PENDING", Assert.Single(rows, value => value.EventType == "LOAN_BORROWED").Status);
    }

    private static OutboxMessageRecord CreateReminder(
        string suffix,
        string loanId,
        string status,
        DateTimeOffset createdAt,
        Guid? leaseId,
        string? lockedBy,
        DateTimeOffset? lockedUntil,
        string eventType = "LOAN_ADVANCE_REMINDER") =>
        new()
        {
            EventId = Guid.NewGuid(),
            DedupeKey = $"loan:{loanId}:{suffix}",
            EventType = eventType,
            AggregateType = "LOAN",
            AggregateId = loanId,
            AggregateVersion = 1,
            CorrelationId = $"correlation:{suffix}",
            PayloadSchemaVersion = 1,
            PayloadKeyVersion = "test-v1",
            PayloadCiphertext = [1, 2, 3],
            CreatedAt = createdAt,
            AvailableAt = createdAt,
            Status = status,
            Attempts = 0,
            LeaseId = leaseId,
            LockedBy = lockedBy,
            LockedUntil = lockedUntil,
        };

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);
}
