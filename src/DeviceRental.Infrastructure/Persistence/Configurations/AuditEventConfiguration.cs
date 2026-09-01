using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRecord>
{
    public void Configure(EntityTypeBuilder<AuditEventRecord> builder)
    {
        builder.ToTable("audit_events", table =>
        {
            table.HasCheckConstraint(
                "ck_audit_events_id_nonzero",
                "event_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_audit_events_actor_kind",
                "actor_kind IN ('USER', 'SYSTEM', 'OPERATIONS')");
            table.HasCheckConstraint(
                "ck_audit_events_actor_tuple",
                "(actor_kind = 'USER' AND actor_user_id IS NOT NULL AND external_actor_identifier IS NULL) OR " +
                "(actor_kind = 'SYSTEM' AND actor_user_id IS NULL AND external_actor_identifier IS NULL) OR " +
                "(actor_kind = 'OPERATIONS' AND actor_user_id IS NULL AND " +
                "external_actor_identifier IS NOT NULL AND btrim(external_actor_identifier) <> '')");
            table.HasCheckConstraint(
                "ck_audit_events_changed_fields_shape",
                "jsonb_typeof(changed_fields_json) = 'object' AND " +
                "changed_fields_json ? 'before' AND jsonb_typeof(changed_fields_json -> 'before') = 'object' AND " +
                "changed_fields_json ? 'after' AND jsonb_typeof(changed_fields_json -> 'after') = 'object' AND " +
                "changed_fields_json - 'before' - 'after' = '{}'::jsonb");
            table.HasCheckConstraint(
                "ck_audit_events_required_text",
                "btrim(event_type) <> '' AND btrim(subject_type) <> '' AND " +
                "btrim(subject_id) <> '' AND btrim(correlation_id) <> ''");
            table.HasCheckConstraint(
                "ck_audit_events_reason_not_blank",
                "reason IS NULL OR btrim(reason) <> ''");
        });

        builder.HasKey(auditEvent => auditEvent.EventId);
        builder.Property(auditEvent => auditEvent.EventId).HasColumnName("event_id");
        builder.Property(auditEvent => auditEvent.ActorKind).HasColumnName("actor_kind").HasMaxLength(24).IsRequired();
        builder.Property(auditEvent => auditEvent.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(auditEvent => auditEvent.ExternalActorIdentifier)
            .HasColumnName("external_actor_identifier")
            .HasMaxLength(256);
        builder.Property(auditEvent => auditEvent.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(auditEvent => auditEvent.SubjectType).HasColumnName("subject_type").HasMaxLength(128).IsRequired();
        builder.Property(auditEvent => auditEvent.SubjectId).HasColumnName("subject_id").HasMaxLength(256).IsRequired();
        builder.Property(auditEvent => auditEvent.ChangedFieldsJson)
            .HasColumnName("changed_fields_json")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(auditEvent => auditEvent.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(auditEvent => auditEvent.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(auditEvent => auditEvent.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(auditEvent => auditEvent.CorrelationId)
            .HasDatabaseName("ix_audit_events_correlation_id");
        builder.HasIndex(auditEvent => new { auditEvent.CreatedAt, auditEvent.EventId })
            .HasDatabaseName("ix_audit_events_created_at_event_id");
        builder.HasIndex(auditEvent => new { auditEvent.ActorUserId, auditEvent.CreatedAt })
            .HasDatabaseName("ix_audit_events_actor_user_id_created_at");
        builder.HasIndex(auditEvent => new { auditEvent.EventType, auditEvent.CreatedAt })
            .HasDatabaseName("ix_audit_events_event_type_created_at");
        builder.HasIndex(auditEvent => new { auditEvent.SubjectType, auditEvent.SubjectId, auditEvent.CreatedAt })
            .HasDatabaseName("ix_audit_events_subject_created_at");
    }
}
