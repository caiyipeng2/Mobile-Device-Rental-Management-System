using System.Text.Json;
using DeviceRental.Domain.Auditing;
using DeviceRental.Domain.Common;
using DeviceRental.Infrastructure.Persistence.Records;

namespace DeviceRental.Infrastructure.Persistence.Mappers;

public static class AuditEventMapper
{
    public static AuditEventRecord ToRecord(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new AuditEventRecord
        {
            EventId = auditEvent.Id,
            ActorKind = auditEvent.ActorKind.ToString().ToUpperInvariant(),
            ActorUserId = auditEvent.ActorUserId,
            ExternalActorIdentifier = auditEvent.ExternalActorIdentifier,
            EventType = auditEvent.EventType,
            SubjectType = auditEvent.ObjectType,
            SubjectId = auditEvent.ObjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["before"] = auditEvent.BeforeValues,
                ["after"] = auditEvent.AfterValues,
            }),
            Reason = auditEvent.Reason?.Value,
            CorrelationId = auditEvent.CorrelationId,
            CreatedAt = auditEvent.OccurredAtUtc,
        };
    }

    public static AuditEvent ToDomain(AuditEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var document = JsonDocument.Parse(record.ChangedFieldsJson);
        var root = document.RootElement;

        return new AuditEvent(
            record.EventId,
            ParseActorKind(record.ActorKind),
            record.ActorUserId,
            record.ExternalActorIdentifier,
            record.EventType,
            record.SubjectType,
            record.SubjectId,
            ReadFields(root, "before"),
            ReadFields(root, "after"),
            record.Reason is null ? null : Reason.From(record.Reason),
            record.CorrelationId,
            record.CreatedAt);
    }

    private static ActorKind ParseActorKind(string value) => value switch
    {
        "USER" => ActorKind.User,
        "SYSTEM" => ActorKind.System,
        "OPERATIONS" => ActorKind.Operations,
        _ => throw new InvalidOperationException($"Unsupported persisted audit actor kind '{value}'."),
    };

    private static IReadOnlyDictionary<string, string?> ReadFields(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var fields) || fields.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Persisted audit JSON is missing object '{propertyName}'.");
        }

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in fields.EnumerateObject())
        {
            result.Add(
                property.Name,
                property.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : property.Value.GetString());
        }

        return result;
    }
}
