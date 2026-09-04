using System.Security.Cryptography;
using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Options;
using DeviceRental.Infrastructure.Persistence;
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

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);
}
