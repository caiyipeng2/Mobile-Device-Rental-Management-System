using DeviceRental.Application.Notifications;
using DeviceRental.Domain.Notifications;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Records;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeviceRental.IntegrationTests.Notifications;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class OutboxProcessorIntegrationTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-006")]
    public async Task ProcessOnceAsync_processes_a_due_loan_reminder_end_to_end()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var borrowerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var borrowedAt = now.AddHours(-4);
        var dueAt = now.AddMinutes(-1);
        var advanceAt = dueAt.AddHours(-2);
        context.Users.Add(CreateUser(borrowerId));
        context.Devices.Add(new DeviceRecord
        {
            Id = deviceId,
            AssetNumber = "WORKER-001",
            ModelName = "Pixel 9",
            Tier = "HIGH",
            ImageId = Guid.NewGuid(),
            ManualState = "NORMAL",
            Version = 1,
            CreatedAt = borrowedAt,
            UpdatedAt = borrowedAt,
        });
        context.LoanPolicyVersions.Add(new LoanPolicyVersionRecord
        {
            Id = policyId,
            VersionNumber = 1,
            DurationMinutes = 1440,
            EffectiveAtUtc = now.AddDays(-1),
            ChangedByUserId = borrowerId,
            Reason = "worker integration test",
        });
        context.Loans.Add(new LoanRecord
        {
            Id = loanId,
            DeviceId = deviceId,
            BorrowerId = borrowerId,
            BorrowedAt = borrowedAt,
            DueAt = dueAt,
            PolicyVersionId = policyId,
            Version = 1,
        });
        context.OutboxMessages.AddRange(
            CreateReminder(loanId, "LOAN_ADVANCE_REMINDER", "advance", borrowedAt, advanceAt),
            CreateReminder(loanId, "LOAN_DUE", "due", borrowedAt, dueAt));
        await context.SaveChangesAsync(cancellationToken);

        var store = new PostgresOutboxStore(context);
        var processor = new OutboxProcessor(
            store,
            new AcceptedNotificationSender(borrowerId));

        var summary = await processor.ProcessOnceAsync(
            now,
            "worker-integration",
            10,
            TimeSpan.FromMinutes(5),
            cancellationToken);

        Assert.Equal(2, summary.Claimed);
        Assert.Equal(2, summary.Started);
        Assert.Equal(2, summary.Processed);
        await using var verify = CreateContext();
        Assert.All(await verify.OutboxMessages.ToListAsync(cancellationToken), value => Assert.Equal("PROCESSED", value.Status));
        var deliveries = await verify.NotificationDeliveries.ToListAsync(cancellationToken);
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, value =>
        {
            Assert.Equal("ACCEPTED", value.Outcome);
            Assert.Contains(value.TemplateIdentifier, new[] { "LOAN_ADVANCE_REMINDER", "LOAN_DUE" });
            Assert.Equal(borrowerId, value.RecipientUserId);
        });
    }

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);

    private static ApplicationUser CreateUser(Guid id) => new()
    {
        Id = id,
        UserName = "worker@example.internal",
        NormalizedUserName = "WORKER@EXAMPLE.INTERNAL",
        Email = "worker@example.internal",
        NormalizedEmail = "WORKER@EXAMPLE.INTERNAL",
        RealName = "Worker User",
        IsActive = true,
        AuthorizationVersion = 1,
        LockoutEnabled = true,
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        UpdatedAt = DateTimeOffset.UtcNow.AddHours(-2),
    };

    private static OutboxMessageRecord CreateReminder(
        Guid loanId,
        string eventType,
        string suffix,
        DateTimeOffset createdAt,
        DateTimeOffset availableAt) => new()
    {
        EventId = Guid.NewGuid(),
        DedupeKey = $"loan:{loanId:D}:v1:{suffix}",
        EventType = eventType,
        AggregateType = "LOAN",
        AggregateId = loanId.ToString("D"),
        AggregateVersion = 1,
        CorrelationId = $"loan:{loanId:D}:v1",
        PayloadSchemaVersion = 1,
        PayloadKeyVersion = "test-key-v1",
        PayloadCiphertext = [1, 2, 3],
        CreatedAt = createdAt,
        AvailableAt = availableAt,
        Status = "PENDING",
        Attempts = 0,
    };

    private sealed class AcceptedNotificationSender(Guid borrowerId) : INotificationSender
    {
        public Task<NotificationSendResult> SendAsync(
            OutboxClaim claim,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NotificationSendResult.Accepted("smtp:250")
                .WithDeliveryMetadata(claim.EventType, borrowerId));
    }
}
