using System.Collections.ObjectModel;
using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Auditing;

public enum ActorKind
{
    User,
    System,
    Operations,
}

public sealed class AuditEvent
{
    public AuditEvent(
        Guid id,
        ActorKind actorKind,
        Guid? actorUserId,
        string? externalActorIdentifier,
        string eventType,
        string objectType,
        string objectId,
        IReadOnlyDictionary<string, string?> beforeValues,
        IReadOnlyDictionary<string, string?> afterValues,
        Reason? reason,
        string correlationId,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(beforeValues);
        ArgumentNullException.ThrowIfNull(afterValues);

        Id = DomainGuard.RequiredId(id, nameof(id));
        ActorKind = DomainGuard.DefinedEnum(actorKind, nameof(actorKind));
        ValidateActor(actorKind, actorUserId, externalActorIdentifier);
        ActorUserId = actorUserId;
        ExternalActorIdentifier = externalActorIdentifier?.Trim();
        EventType = DomainGuard.RequiredText(eventType, nameof(eventType));
        ObjectType = DomainGuard.RequiredText(objectType, nameof(objectType));
        ObjectId = DomainGuard.RequiredText(objectId, nameof(objectId));
        BeforeValues = CopyFields(beforeValues, nameof(beforeValues));
        AfterValues = CopyFields(afterValues, nameof(afterValues));
        Reason = reason;
        CorrelationId = DomainGuard.RequiredText(correlationId, nameof(correlationId));
        OccurredAtUtc = DomainGuard.Utc(occurredAtUtc);
    }

    public Guid Id { get; }

    public ActorKind ActorKind { get; }

    public Guid? ActorUserId { get; }

    public string? ExternalActorIdentifier { get; }

    public string EventType { get; }

    public string ObjectType { get; }

    public string ObjectId { get; }

    public IReadOnlyDictionary<string, string?> BeforeValues { get; }

    public IReadOnlyDictionary<string, string?> AfterValues { get; }

    public Reason? Reason { get; }

    public string CorrelationId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    private static void ValidateActor(
        ActorKind actorKind,
        Guid? actorUserId,
        string? externalActorIdentifier)
    {
        if (actorKind == Auditing.ActorKind.User)
        {
            if (actorUserId is null || actorUserId == Guid.Empty)
            {
                throw new ArgumentException("A user actor requires a non-empty user identifier.", nameof(actorUserId));
            }

            if (externalActorIdentifier is not null)
            {
                throw new ArgumentException("A user actor cannot have an external actor identifier.", nameof(externalActorIdentifier));
            }

            return;
        }

        if (actorUserId is not null)
        {
            throw new ArgumentException("A non-user actor cannot have a user identifier.", nameof(actorUserId));
        }

        if (actorKind == Auditing.ActorKind.System)
        {
            if (externalActorIdentifier is not null)
            {
                throw new ArgumentException("A system actor cannot have an external actor identifier.", nameof(externalActorIdentifier));
            }

            return;
        }

        DomainGuard.RequiredText(externalActorIdentifier, nameof(externalActorIdentifier));
    }

    private static IReadOnlyDictionary<string, string?> CopyFields(
        IReadOnlyDictionary<string, string?> source,
        string parameterName)
    {
        var copy = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in source)
        {
            var normalizedKey = DomainGuard.RequiredText(key, parameterName);
            if (!copy.TryAdd(normalizedKey, value))
            {
                throw new ArgumentException("Audit field names must be unique after trimming.", parameterName);
            }
        }

        return new ReadOnlyDictionary<string, string?>(copy);
    }
}
