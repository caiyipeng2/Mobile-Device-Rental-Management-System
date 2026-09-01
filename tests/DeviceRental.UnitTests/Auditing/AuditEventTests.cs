using DeviceRental.Domain.Auditing;
using DeviceRental.Domain.Common;
using Xunit;

namespace DeviceRental.UnitTests.Auditing;

public sealed class AuditEventTests
{
    [Fact]
    public void Constructor_AcceptsUserActorAndCopiesWhitelistedFieldValues()
    {
        var actorId = Guid.NewGuid();
        var before = new Dictionary<string, string?> { ["status"] = "NORMAL" };
        var auditEvent = new AuditEvent(
            Guid.NewGuid(),
            ActorKind.User,
            actorId,
            null,
            "DeviceDisabled",
            "Device",
            Guid.NewGuid().ToString("D"),
            before,
            new Dictionary<string, string?> { ["status"] = "TEMP_DISABLED" },
            Reason.From("damaged"),
            "correlation-1",
            new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.FromHours(8)));
        before["status"] = "MUTATED";

        Assert.Equal(actorId, auditEvent.ActorUserId);
        Assert.Null(auditEvent.ExternalActorIdentifier);
        Assert.Equal("NORMAL", auditEvent.BeforeValues["status"]);
        Assert.Equal(TimeSpan.Zero, auditEvent.OccurredAtUtc.Offset);
    }

    [Fact]
    public void Constructor_AcceptsSystemActorWithoutIdentity()
    {
        var auditEvent = Create(ActorKind.System, null, null);

        Assert.Null(auditEvent.ActorUserId);
        Assert.Null(auditEvent.ExternalActorIdentifier);
    }

    [Fact]
    public void Constructor_AcceptsOperationsActorWithExternalIdentifier()
    {
        var auditEvent = Create(ActorKind.Operations, null, "admin-cli/operator-123");

        Assert.Null(auditEvent.ActorUserId);
        Assert.Equal("admin-cli/operator-123", auditEvent.ExternalActorIdentifier);
    }

    [Fact]
    public void Constructor_EnforcesActorIdentityInvariant()
    {
        Assert.Throws<ArgumentException>(() => Create(ActorKind.User, null, null));
        Assert.Throws<ArgumentException>(() => Create(ActorKind.User, Guid.NewGuid(), "external"));
        Assert.Throws<ArgumentException>(() => Create(ActorKind.System, Guid.NewGuid(), "worker"));
        Assert.Throws<ArgumentException>(() => Create(ActorKind.System, null, "worker"));
        Assert.Throws<ArgumentException>(() => Create(ActorKind.Operations, null, " "));
    }

    [Fact]
    public void Constructor_RejectsEmptyEventAndObjectIdentifiersOrFieldNames()
    {
        Assert.Throws<ArgumentException>(() => new AuditEvent(
            Guid.Empty,
            ActorKind.System,
            null,
            null,
            "event",
            "object",
            "id",
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            null,
            "correlation",
            DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new AuditEvent(
            Guid.NewGuid(),
            ActorKind.System,
            null,
            null,
            "event",
            "object",
            "id",
            new Dictionary<string, string?> { [" "] = "bad" },
            new Dictionary<string, string?>(),
            null,
            "correlation",
            DateTimeOffset.UtcNow));
    }

    private static AuditEvent Create(ActorKind kind, Guid? userId, string? externalIdentifier) =>
        new(
            Guid.NewGuid(),
            kind,
            userId,
            externalIdentifier,
            "Event",
            "Object",
            "object-id",
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            null,
            "correlation",
            DateTimeOffset.UtcNow);
}
