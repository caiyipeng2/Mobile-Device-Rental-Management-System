using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageRecord>
{
    public void Configure(EntityTypeBuilder<OutboxMessageRecord> builder)
    {
        builder.ToTable("outbox_messages", table =>
        {
            table.HasCheckConstraint(
                "ck_outbox_messages_id_nonzero",
                "event_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_outbox_messages_status",
                "status IN ('PENDING', 'CLAIMED', 'SENDING', 'PROCESSED', " +
                "'DEAD_LETTER', 'REVIEW_REQUIRED', 'CANCELLED')");
            table.HasCheckConstraint(
                "ck_outbox_messages_required_values",
                "btrim(dedupe_key) <> '' AND btrim(event_type) <> '' AND " +
                "btrim(aggregate_type) <> '' AND btrim(aggregate_id) <> '' AND " +
                "aggregate_version > 0 AND btrim(correlation_id) <> ''");
            table.HasCheckConstraint(
                "ck_outbox_messages_payload_tuple",
                "(payload_schema_version IS NULL AND payload_key_version IS NULL AND payload_ciphertext IS NULL) OR " +
                "(payload_schema_version > 0 AND payload_key_version IS NOT NULL AND " +
                "btrim(payload_key_version) <> '' AND payload_ciphertext IS NOT NULL AND " +
                "octet_length(payload_ciphertext) > 0)");
            table.HasCheckConstraint(
                "ck_outbox_messages_lease_tuple",
                "(lease_id IS NULL AND locked_by IS NULL AND locked_until IS NULL) OR " +
                "(lease_id IS NOT NULL AND lease_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "locked_by IS NOT NULL AND btrim(locked_by) <> '' AND locked_until IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_outbox_messages_time_order",
                "available_at >= created_at AND attempts >= 0 AND " +
                "(locked_until IS NULL OR locked_until > created_at) AND " +
                "(sending_started_at IS NULL OR (sending_started_at >= available_at AND " +
                "sending_started_at < locked_until)) AND " +
                "(processed_at IS NULL OR processed_at >= sending_started_at) AND " +
                "(failed_at IS NULL OR failed_at >= sending_started_at) AND " +
                "(canceled_at IS NULL OR canceled_at >= created_at) AND " +
                "(payload_purged_at IS NULL OR payload_purged_at >= COALESCE(processed_at, canceled_at, failed_at))");
            table.HasCheckConstraint(
                "ck_outbox_messages_state_tuple",
                StateTupleConstraint);
            table.HasCheckConstraint(
                "ck_outbox_messages_payload_retention",
                PayloadRetentionConstraint);
        });

        builder.HasKey(message => message.EventId);
        builder.Property(message => message.EventId).HasColumnName("event_id");
        builder.Property(message => message.DedupeKey).HasColumnName("dedupe_key").HasMaxLength(256).IsRequired();
        builder.Property(message => message.EventType).HasColumnName("event_type").HasMaxLength(128).IsRequired();
        builder.Property(message => message.AggregateType).HasColumnName("aggregate_type").HasMaxLength(128).IsRequired();
        builder.Property(message => message.AggregateId).HasColumnName("aggregate_id").HasMaxLength(256).IsRequired();
        builder.Property(message => message.AggregateVersion).HasColumnName("aggregate_version").IsRequired();
        builder.Property(message => message.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128).IsRequired();
        builder.Property(message => message.PayloadSchemaVersion).HasColumnName("payload_schema_version");
        builder.Property(message => message.PayloadKeyVersion).HasColumnName("payload_key_version").HasMaxLength(128);
        builder.Property(message => message.PayloadCiphertext).HasColumnName("payload_ciphertext");
        builder.Property(message => message.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(message => message.AvailableAt).HasColumnName("available_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(message => message.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(message => message.Attempts).HasColumnName("attempts").HasDefaultValue(0).IsRequired();
        builder.Property(message => message.LeaseId).HasColumnName("lease_id");
        builder.Property(message => message.LockedBy).HasColumnName("locked_by").HasMaxLength(256);
        builder.Property(message => message.LockedUntil).HasColumnName("locked_until").HasColumnType("timestamp with time zone");
        builder.Property(message => message.SendingStartedAt).HasColumnName("sending_started_at").HasColumnType("timestamp with time zone");
        builder.Property(message => message.ProcessedAt).HasColumnName("processed_at").HasColumnType("timestamp with time zone");
        builder.Property(message => message.CanceledAt).HasColumnName("canceled_at").HasColumnType("timestamp with time zone");
        builder.Property(message => message.FailedAt).HasColumnName("failed_at").HasColumnType("timestamp with time zone");
        builder.Property(message => message.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(message => message.PayloadPurgedAt).HasColumnName("payload_purged_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(message => message.DedupeKey)
            .HasDatabaseName("ux_outbox_messages_dedupe_key")
            .IsUnique();
        builder.HasIndex(message => message.CorrelationId)
            .HasDatabaseName("ix_outbox_messages_correlation_id");
        builder.HasIndex(message => new
            {
                message.AggregateType,
                message.AggregateId,
                message.AggregateVersion,
            })
            .HasDatabaseName("ix_outbox_messages_aggregate_version");
        builder.HasIndex(message => new { message.Status, message.AvailableAt })
            .HasDatabaseName("ix_outbox_messages_pending_due")
            .HasFilter("status = 'PENDING'");
        builder.HasIndex(message => new { message.Status, message.LockedUntil })
            .HasDatabaseName("ix_outbox_messages_claimed_lease")
            .HasFilter("status = 'CLAIMED'");
    }

    private const string CompleteLease =
        "lease_id IS NOT NULL AND locked_by IS NOT NULL AND locked_until IS NOT NULL";

    private const string NoLease =
        "lease_id IS NULL AND locked_by IS NULL AND locked_until IS NULL";

    private const string NoTerminal =
        "processed_at IS NULL AND canceled_at IS NULL AND failed_at IS NULL";

    private const string StateTupleConstraint =
        "(status = 'PENDING' AND " + NoLease + " AND sending_started_at IS NULL AND " + NoTerminal +
        " AND ((attempts = 0 AND last_error IS NULL) OR (attempts > 0 AND last_error IS NOT NULL AND btrim(last_error) <> ''))) OR " +
        "(status = 'CLAIMED' AND " + CompleteLease + " AND sending_started_at IS NULL AND " + NoTerminal +
        " AND ((attempts = 0 AND last_error IS NULL) OR (attempts > 0 AND last_error IS NOT NULL AND btrim(last_error) <> ''))) OR " +
        "(status = 'SENDING' AND " + CompleteLease + " AND attempts > 0 AND sending_started_at IS NOT NULL AND " +
        NoTerminal + " AND last_error IS NULL) OR " +
        "(status = 'PROCESSED' AND " + CompleteLease + " AND attempts > 0 AND sending_started_at IS NOT NULL AND " +
        "processed_at IS NOT NULL AND canceled_at IS NULL AND failed_at IS NULL AND last_error IS NULL) OR " +
        "(status IN ('DEAD_LETTER', 'REVIEW_REQUIRED') AND " + CompleteLease +
        " AND attempts > 0 AND sending_started_at IS NOT NULL AND processed_at IS NULL AND canceled_at IS NULL AND " +
        "failed_at IS NOT NULL AND last_error IS NOT NULL AND btrim(last_error) <> '') OR " +
        "(status = 'CANCELLED' AND sending_started_at IS NULL AND processed_at IS NULL AND " +
        "canceled_at IS NOT NULL AND failed_at IS NULL AND last_error IS NULL AND (" + NoLease + " OR (" + CompleteLease + ")))";

    private const string CompletePayload =
        "payload_schema_version IS NOT NULL AND payload_key_version IS NOT NULL AND payload_ciphertext IS NOT NULL";

    private const string NoPayload =
        "payload_schema_version IS NULL AND payload_key_version IS NULL AND payload_ciphertext IS NULL";

    private const string PayloadRetentionConstraint =
        "(status IN ('PENDING', 'CLAIMED', 'SENDING') AND " + CompletePayload + " AND payload_purged_at IS NULL) OR " +
        "(status IN ('PROCESSED', 'DEAD_LETTER', 'REVIEW_REQUIRED', 'CANCELLED') AND ((" +
        CompletePayload + " AND payload_purged_at IS NULL) OR (" + NoPayload + " AND payload_purged_at IS NOT NULL)))";
}
