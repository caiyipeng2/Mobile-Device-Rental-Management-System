using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "device_rental",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_actor_identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    subject_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    changed_fields_json = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.event_id);
                    table.CheckConstraint("ck_audit_events_actor_kind", "actor_kind IN ('USER', 'SYSTEM', 'OPERATIONS')");
                    table.CheckConstraint("ck_audit_events_actor_tuple", "(actor_kind = 'USER' AND actor_user_id IS NOT NULL AND external_actor_identifier IS NULL) OR (actor_kind = 'SYSTEM' AND actor_user_id IS NULL AND external_actor_identifier IS NULL) OR (actor_kind = 'OPERATIONS' AND actor_user_id IS NULL AND external_actor_identifier IS NOT NULL AND btrim(external_actor_identifier) <> '')");
                    table.CheckConstraint("ck_audit_events_changed_fields_shape", "jsonb_typeof(changed_fields_json) = 'object' AND changed_fields_json ? 'before' AND jsonb_typeof(changed_fields_json -> 'before') = 'object' AND changed_fields_json ? 'after' AND jsonb_typeof(changed_fields_json -> 'after') = 'object' AND changed_fields_json - 'before' - 'after' = '{}'::jsonb");
                    table.CheckConstraint("ck_audit_events_id_nonzero", "event_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_audit_events_reason_not_blank", "reason IS NULL OR btrim(reason) <> ''");
                    table.CheckConstraint("ck_audit_events_required_text", "btrim(event_type) <> '' AND btrim(subject_type) <> '' AND btrim(subject_id) <> '' AND btrim(correlation_id) <> ''");
                    table.ForeignKey(
                        name: "FK_audit_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "device_rental",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "device_rental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tier = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    image_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    temporary_unavailable_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                    table.CheckConstraint("ck_devices_id_nonzero", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_devices_manual_state", "manual_state IN ('NORMAL', 'TEMPORARILY_DISABLED')");
                    table.CheckConstraint("ck_devices_required_text", "btrim(asset_number) <> '' AND btrim(model_name) <> ''");
                    table.CheckConstraint("ck_devices_tier", "tier IN ('LOW', 'MID', 'HIGH')");
                    table.CheckConstraint("ck_devices_timestamps", "updated_at >= created_at");
                    table.CheckConstraint("ck_devices_unavailable_reason", "(manual_state = 'NORMAL' AND temporary_unavailable_reason IS NULL) OR (manual_state = 'TEMPORARILY_DISABLED' AND temporary_unavailable_reason IS NOT NULL AND btrim(temporary_unavailable_reason) <> '')");
                    table.CheckConstraint("ck_devices_version_positive", "version > 0");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "device_rental",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload_schema_version = table.Column<int>(type: "integer", nullable: true),
                    payload_key_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    payload_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    locked_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sending_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    canceled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    payload_purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.event_id);
                    table.CheckConstraint("ck_outbox_messages_id_nonzero", "event_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_outbox_messages_lease_tuple", "(lease_id IS NULL AND locked_by IS NULL AND locked_until IS NULL) OR (lease_id IS NOT NULL AND lease_id <> '00000000-0000-0000-0000-000000000000'::uuid AND locked_by IS NOT NULL AND btrim(locked_by) <> '' AND locked_until IS NOT NULL)");
                    table.CheckConstraint("ck_outbox_messages_payload_retention", "(status IN ('PENDING', 'CLAIMED', 'SENDING') AND payload_schema_version IS NOT NULL AND payload_key_version IS NOT NULL AND payload_ciphertext IS NOT NULL AND payload_purged_at IS NULL) OR (status IN ('PROCESSED', 'DEAD_LETTER', 'REVIEW_REQUIRED', 'CANCELLED') AND ((payload_schema_version IS NOT NULL AND payload_key_version IS NOT NULL AND payload_ciphertext IS NOT NULL AND payload_purged_at IS NULL) OR (payload_schema_version IS NULL AND payload_key_version IS NULL AND payload_ciphertext IS NULL AND payload_purged_at IS NOT NULL)))");
                    table.CheckConstraint("ck_outbox_messages_payload_tuple", "(payload_schema_version IS NULL AND payload_key_version IS NULL AND payload_ciphertext IS NULL) OR (payload_schema_version > 0 AND payload_key_version IS NOT NULL AND btrim(payload_key_version) <> '' AND payload_ciphertext IS NOT NULL AND octet_length(payload_ciphertext) > 0)");
                    table.CheckConstraint("ck_outbox_messages_required_values", "btrim(dedupe_key) <> '' AND btrim(event_type) <> '' AND btrim(aggregate_type) <> '' AND btrim(aggregate_id) <> '' AND aggregate_version > 0 AND btrim(correlation_id) <> ''");
                    table.CheckConstraint("ck_outbox_messages_state_tuple", "(status = 'PENDING' AND lease_id IS NULL AND locked_by IS NULL AND locked_until IS NULL AND sending_started_at IS NULL AND processed_at IS NULL AND canceled_at IS NULL AND failed_at IS NULL AND ((attempts = 0 AND last_error IS NULL) OR (attempts > 0 AND last_error IS NOT NULL AND btrim(last_error) <> ''))) OR (status = 'CLAIMED' AND lease_id IS NOT NULL AND locked_by IS NOT NULL AND locked_until IS NOT NULL AND sending_started_at IS NULL AND processed_at IS NULL AND canceled_at IS NULL AND failed_at IS NULL AND ((attempts = 0 AND last_error IS NULL) OR (attempts > 0 AND last_error IS NOT NULL AND btrim(last_error) <> ''))) OR (status = 'SENDING' AND lease_id IS NOT NULL AND locked_by IS NOT NULL AND locked_until IS NOT NULL AND attempts > 0 AND sending_started_at IS NOT NULL AND processed_at IS NULL AND canceled_at IS NULL AND failed_at IS NULL AND last_error IS NULL) OR (status = 'PROCESSED' AND lease_id IS NOT NULL AND locked_by IS NOT NULL AND locked_until IS NOT NULL AND attempts > 0 AND sending_started_at IS NOT NULL AND processed_at IS NOT NULL AND canceled_at IS NULL AND failed_at IS NULL AND last_error IS NULL) OR (status IN ('DEAD_LETTER', 'REVIEW_REQUIRED') AND lease_id IS NOT NULL AND locked_by IS NOT NULL AND locked_until IS NOT NULL AND attempts > 0 AND sending_started_at IS NOT NULL AND processed_at IS NULL AND canceled_at IS NULL AND failed_at IS NOT NULL AND last_error IS NOT NULL AND btrim(last_error) <> '') OR (status = 'CANCELLED' AND sending_started_at IS NULL AND processed_at IS NULL AND canceled_at IS NOT NULL AND failed_at IS NULL AND last_error IS NULL AND (lease_id IS NULL AND locked_by IS NULL AND locked_until IS NULL OR (lease_id IS NOT NULL AND locked_by IS NOT NULL AND locked_until IS NOT NULL)))");
                    table.CheckConstraint("ck_outbox_messages_status", "status IN ('PENDING', 'CLAIMED', 'SENDING', 'PROCESSED', 'DEAD_LETTER', 'REVIEW_REQUIRED', 'CANCELLED')");
                    table.CheckConstraint("ck_outbox_messages_time_order", "available_at >= created_at AND attempts >= 0 AND (locked_until IS NULL OR locked_until > created_at) AND (sending_started_at IS NULL OR (sending_started_at >= available_at AND sending_started_at < locked_until)) AND (processed_at IS NULL OR processed_at >= sending_started_at) AND (failed_at IS NULL OR failed_at >= sending_started_at) AND (canceled_at IS NULL OR canceled_at >= created_at) AND (payload_purged_at IS NULL OR payload_purged_at >= COALESCE(processed_at, canceled_at, failed_at))");
                });

            migrationBuilder.CreateTable(
                name: "loans",
                schema: "device_rental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrowed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    returned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    return_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loans", x => x.id);
                    table.CheckConstraint("ck_loans_id_nonzero", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_loans_parties_nonzero", "device_id <> '00000000-0000-0000-0000-000000000000'::uuid AND borrower_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_loans_return_tuple", "(returned_at IS NULL AND returned_by_user_id IS NULL AND return_kind IS NULL AND return_reason IS NULL) OR (returned_at IS NOT NULL AND returned_by_user_id IS NOT NULL AND returned_by_user_id <> '00000000-0000-0000-0000-000000000000'::uuid AND return_kind IN ('SELF', 'FORCED') AND ((return_kind = 'SELF' AND returned_by_user_id = borrower_id AND return_reason IS NULL) OR (return_kind = 'FORCED' AND return_reason IS NOT NULL AND btrim(return_reason) <> '')))");
                    table.CheckConstraint("ck_loans_time_order", "due_at > borrowed_at AND (returned_at IS NULL OR returned_at >= borrowed_at)");
                    table.CheckConstraint("ck_loans_version_positive", "version > 0");
                    table.ForeignKey(
                        name: "FK_loans_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "device_rental",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loans_users_borrower_id",
                        column: x => x.borrower_id,
                        principalSchema: "device_rental",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loans_users_returned_by_user_id",
                        column: x => x.returned_by_user_id,
                        principalSchema: "device_rental",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor_user_id_created_at",
                schema: "device_rental",
                table: "audit_events",
                columns: new[] { "actor_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_correlation_id",
                schema: "device_rental",
                table: "audit_events",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_created_at_event_id",
                schema: "device_rental",
                table: "audit_events",
                columns: new[] { "created_at", "event_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_event_type_created_at",
                schema: "device_rental",
                table: "audit_events",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_subject_created_at",
                schema: "device_rental",
                table: "audit_events",
                columns: new[] { "subject_type", "subject_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_devices_archive_tier",
                schema: "device_rental",
                table: "devices",
                columns: new[] { "is_archived", "tier" });

            migrationBuilder.CreateIndex(
                name: "ix_devices_manual_state",
                schema: "device_rental",
                table: "devices",
                column: "manual_state");

            migrationBuilder.CreateIndex(
                name: "ux_devices_asset_number",
                schema: "device_rental",
                table: "devices",
                column: "asset_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loans_borrower_borrowed_at",
                schema: "device_rental",
                table: "loans",
                columns: new[] { "borrower_id", "borrowed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_due_returned",
                schema: "device_rental",
                table: "loans",
                columns: new[] { "due_at", "returned_at" });

            migrationBuilder.CreateIndex(
                name: "IX_loans_returned_by_user_id",
                schema: "device_rental",
                table: "loans",
                column: "returned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_loans_open_device",
                schema: "device_rental",
                table: "loans",
                column: "device_id",
                unique: true,
                filter: "returned_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_aggregate_version",
                schema: "device_rental",
                table: "outbox_messages",
                columns: new[] { "aggregate_type", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_correlation_id",
                schema: "device_rental",
                table: "outbox_messages",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ux_outbox_messages_dedupe_key",
                schema: "device_rental",
                table: "outbox_messages",
                column: "dedupe_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "device_rental");

            migrationBuilder.DropTable(
                name: "loans",
                schema: "device_rental");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "device_rental");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "device_rental");
        }
    }
}
