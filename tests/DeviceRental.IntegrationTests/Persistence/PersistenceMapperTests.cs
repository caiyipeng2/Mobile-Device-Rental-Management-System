using DeviceRental.Domain.Auditing;
using DeviceRental.Domain.Common;
using DeviceRental.Domain.Notifications;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeviceRental.IntegrationTests.Persistence;

public sealed class PersistenceMapperTests
{
    private static readonly DateTimeOffset CreatedAt =
        DateTimeOffset.Parse("2026-09-01T10:00:00Z");

    public static TheoryData<ActorKind, Guid?, string?> AuditActors => new()
    {
        { ActorKind.User, Guid.NewGuid(), null },
        { ActorKind.System, null, null },
        { ActorKind.Operations, null, "admin-cli/deployment-42" },
    };

    [Theory]
    [MemberData(nameof(AuditActors))]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUDIT-001")]
    public void AuditEvent_RoundTripsEveryActorAndWhitelistedChangeDocument(
        ActorKind actorKind,
        Guid? actorUserId,
        string? externalActorIdentifier)
    {
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            actorKind,
            actorUserId,
            externalActorIdentifier,
            "DEVICE_DISABLED",
            "DEVICE",
            "device-42",
            new Dictionary<string, string?> { ["status"] = "NORMAL", ["reason"] = null },
            new Dictionary<string, string?> { ["status"] = "TEMP_DISABLED" },
            Reason.From("screen damaged"),
            "correlation-42",
            CreatedAt);

        var record = AuditEventMapper.ToRecord(auditEvent);
        var roundTripped = AuditEventMapper.ToDomain(record);

        Assert.Equal(auditEvent.Id, roundTripped.Id);
        Assert.Equal(actorKind, roundTripped.ActorKind);
        Assert.Equal(actorUserId, roundTripped.ActorUserId);
        Assert.Equal(externalActorIdentifier, roundTripped.ExternalActorIdentifier);
        Assert.Equal("NORMAL", roundTripped.BeforeValues["status"]);
        Assert.Null(roundTripped.BeforeValues["reason"]);
        Assert.Equal("TEMP_DISABLED", roundTripped.AfterValues["status"]);
        Assert.Equal(Reason.From("screen damaged"), roundTripped.Reason);
        Assert.Equal("correlation-42", roundTripped.CorrelationId);
        Assert.Equal(CreatedAt, roundTripped.OccurredAtUtc);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public void PendingOutbox_RoundTripCopiesCiphertextInBothDirections()
    {
        var message = OutboxMessage.Pending(
            Guid.NewGuid(),
            "loan-42:borrowed",
            "LOAN_BORROWED",
            "LOAN",
            "loan-42",
            1,
            "correlation-42",
            new EncryptedPayload(2, "key-v2", [1, 2, 3]),
            CreatedAt,
            CreatedAt.AddMinutes(1));

        var firstRecord = OutboxMessageMapper.ToRecord(message);
        firstRecord.PayloadCiphertext![0] = 9;
        Assert.Equal([1, 2, 3], message.Payload!.Ciphertext);

        var secondRecord = OutboxMessageMapper.ToRecord(message);
        var roundTripped = OutboxMessageMapper.ToDomain(secondRecord);
        secondRecord.PayloadCiphertext![1] = 9;

        Assert.Equal(OutboxStatus.Pending, roundTripped.Status);
        Assert.Equal([1, 2, 3], roundTripped.Payload!.Ciphertext);
        Assert.Equal("correlation-42", roundTripped.CorrelationId);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-005")]
    public void TerminalPurgedOutbox_RoundTripsWithoutRecreatingPayload()
    {
        var processedAt = CreatedAt.AddMinutes(2);
        var processed = new OutboxMessage(
            Guid.NewGuid(),
            "loan-42:due",
            "LOAN_DUE",
            "LOAN",
            "loan-42",
            2,
            "correlation-42",
            new EncryptedPayload(1, "key-v1", [4, 5, 6]),
            CreatedAt,
            CreatedAt,
            OutboxStatus.Processed,
            1,
            Guid.NewGuid(),
            "worker-1",
            CreatedAt.AddMinutes(10),
            CreatedAt.AddMinutes(1),
            processedAt,
            null,
            null,
            null)
            .PurgePayload(processedAt.AddMinutes(1));

        var record = OutboxMessageMapper.ToRecord(processed);
        var roundTripped = OutboxMessageMapper.ToDomain(record);

        Assert.Equal(OutboxStatus.Processed, roundTripped.Status);
        Assert.Null(roundTripped.Payload);
        Assert.Equal(processedAt.AddMinutes(1), roundTripped.PayloadPurgedAtUtc);
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUDIT-001")]
    public void AuditMapper_RejectsUnknownPersistedActorToken()
    {
        var auditRecord = AuditEventMapper.ToRecord(CreateSystemAudit());
        auditRecord.ActorKind = "SERVICE";
        Assert.Throws<InvalidOperationException>(() => AuditEventMapper.ToDomain(auditRecord));
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public void OutboxMapper_RejectsUnknownPersistedStatusToken()
    {
        var outboxRecord = OutboxMessageMapper.ToRecord(CreatePendingOutbox());
        outboxRecord.Status = "UNKNOWN";
        Assert.Throws<InvalidOperationException>(() => OutboxMessageMapper.ToDomain(outboxRecord));
    }

    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public void OutboxMapper_RejectsIncompletePersistedPayloadTuple()
    {
        var record = OutboxMessageMapper.ToRecord(CreatePendingOutbox());
        record.PayloadSchemaVersion = null;

        Assert.Throws<InvalidOperationException>(() => OutboxMessageMapper.ToDomain(record));
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    [Trait("Category", "Database")]
    [Trait("Requirement", "REQ-AUDIT-003")]
    public async Task DbContext_AllSaveChangesOverloadsRejectAuditHistoryMutationBeforeConnecting(
        EntityState state)
    {
        using (var context = CreateContextWithAuditState(state))
        {
            Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        }

        using (var context = CreateContextWithAuditState(state))
        {
            Assert.Throws<InvalidOperationException>(() => context.SaveChanges(false));
        }

        await using (var context = CreateContextWithAuditState(state))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var context = CreateContextWithAuditState(state))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.SaveChangesAsync(false, TestContext.Current.CancellationToken));
        }
    }

    private static DeviceRentalDbContext CreateContextWithAuditState(EntityState state)
    {
        var options = new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=unreachable;Timeout=1")
            .Options;
        var context = new DeviceRentalDbContext(options);
        var record = AuditEventMapper.ToRecord(CreateSystemAudit());
        context.Attach(record);
        context.Entry(record).State = state;
        return context;
    }

    private static AuditEvent CreateSystemAudit() => new(
        Guid.NewGuid(),
        ActorKind.System,
        null,
        null,
        "DEVICE_CREATED",
        "DEVICE",
        "device-42",
        new Dictionary<string, string?>(),
        new Dictionary<string, string?> { ["status"] = "AVAILABLE" },
        null,
        "correlation-42",
        CreatedAt);

    private static OutboxMessage CreatePendingOutbox() => OutboxMessage.Pending(
        Guid.NewGuid(),
        "loan-42:borrowed",
        "LOAN_BORROWED",
        "LOAN",
        "loan-42",
        1,
        "correlation-42",
        new EncryptedPayload(1, "key-v1", [1, 2, 3]),
        CreatedAt,
        CreatedAt);
}
