using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class NotificationDeliveryRecordConfiguration : IEntityTypeConfiguration<NotificationDeliveryRecord>
{
    public void Configure(EntityTypeBuilder<NotificationDeliveryRecord> builder)
    {
        builder.ToTable("notification_deliveries", table =>
        {
            table.HasCheckConstraint(
                "ck_notification_deliveries_id_nonzero",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_notification_deliveries_event_nonzero",
                "event_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_notification_deliveries_required_text",
                "btrim(dedupe_key) <> '' AND btrim(channel) <> '' AND " +
                "btrim(template_identifier) <> '' AND attempt_number > 0");
            table.HasCheckConstraint(
                "ck_notification_deliveries_recipient_tuple",
                "(recipient_user_id IS NOT NULL AND recipient_user_id <> '00000000-0000-0000-0000-000000000000'::uuid AND recipient_key_version IS NULL AND recipient_ciphertext IS NULL) OR " +
                "(recipient_user_id IS NULL AND recipient_key_version IS NOT NULL AND btrim(recipient_key_version) <> '' AND recipient_ciphertext IS NOT NULL AND octet_length(recipient_ciphertext) > 0)");
            table.HasCheckConstraint(
                "ck_notification_deliveries_outcome_tuple",
                "(outcome = 'ACCEPTED' AND acceptance_evidence = 'ACCEPTED' AND acceptance_evidence_reference IS NOT NULL AND btrim(acceptance_evidence_reference) <> '' AND sanitized_error IS NULL) OR " +
                "(outcome IN ('TRANSIENT_NOT_ACCEPTED', 'PERMANENT_REJECTED', 'ACCEPTANCE_UNKNOWN') AND sanitized_error IS NOT NULL AND btrim(sanitized_error) <> '' AND acceptance_evidence_reference IS NULL)");
            table.HasCheckConstraint(
                "ck_notification_deliveries_time_order",
                "completed_at >= started_at");
        });

        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).HasColumnName("id");
        builder.Property(delivery => delivery.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(delivery => delivery.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(256).IsRequired();
        builder.Property(delivery => delivery.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(delivery => delivery.RecipientKeyVersion).HasColumnName("recipient_key_version").HasMaxLength(128);
        builder.Property(delivery => delivery.RecipientCiphertext).HasColumnName("recipient_ciphertext");
        builder.Property(delivery => delivery.Channel).HasColumnName("channel").HasMaxLength(32).IsRequired();
        builder.Property(delivery => delivery.TemplateIdentifier).HasColumnName("template_identifier").HasMaxLength(128).IsRequired();
        builder.Property(delivery => delivery.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(delivery => delivery.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(delivery => delivery.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(delivery => delivery.Outcome).HasColumnName("outcome").HasMaxLength(32).IsRequired();
        builder.Property(delivery => delivery.AcceptanceEvidence).HasColumnName("acceptance_evidence").HasMaxLength(16).IsRequired();
        builder.Property(delivery => delivery.AcceptanceEvidenceReference).HasColumnName("acceptance_evidence_reference").HasMaxLength(256);
        builder.Property(delivery => delivery.SanitizedError).HasColumnName("sanitized_error").HasMaxLength(2000);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(delivery => delivery.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OutboxMessageRecord>()
            .WithMany()
            .HasForeignKey(delivery => delivery.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(delivery => new { delivery.EventId, delivery.DedupeKey })
            .HasDatabaseName("ux_notification_deliveries_event_dedupe")
            .IsUnique();
        builder.HasIndex(delivery => delivery.RecipientUserId)
            .HasDatabaseName("ix_notification_deliveries_recipient");
    }
}
