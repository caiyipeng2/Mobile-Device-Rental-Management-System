using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationDeliveryAndOperationalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                schema: "device_rental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_key_version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    recipient_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    template_identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    acceptance_evidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    acceptance_evidence_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    sanitized_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.id);
                    table.CheckConstraint("ck_notification_deliveries_event_nonzero", "event_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_notification_deliveries_id_nonzero", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_notification_deliveries_outcome_tuple", "(outcome = 'ACCEPTED' AND acceptance_evidence = 'ACCEPTED' AND acceptance_evidence_reference IS NOT NULL AND btrim(acceptance_evidence_reference) <> '' AND sanitized_error IS NULL) OR (outcome IN ('TRANSIENT_NOT_ACCEPTED', 'PERMANENT_REJECTED', 'ACCEPTANCE_UNKNOWN') AND sanitized_error IS NOT NULL AND btrim(sanitized_error) <> '' AND acceptance_evidence_reference IS NULL)");
                    table.CheckConstraint("ck_notification_deliveries_recipient_tuple", "(recipient_user_id IS NOT NULL AND recipient_user_id <> '00000000-0000-0000-0000-000000000000'::uuid AND recipient_key_version IS NULL AND recipient_ciphertext IS NULL) OR (recipient_user_id IS NULL AND recipient_key_version IS NOT NULL AND btrim(recipient_key_version) <> '' AND recipient_ciphertext IS NOT NULL AND octet_length(recipient_ciphertext) > 0)");
                    table.CheckConstraint("ck_notification_deliveries_required_text", "btrim(dedupe_key) <> '' AND btrim(channel) <> '' AND btrim(template_identifier) <> '' AND attempt_number > 0");
                    table.CheckConstraint("ck_notification_deliveries_time_order", "completed_at >= started_at");
                    table.ForeignKey(
                        name: "FK_notification_deliveries_outbox_messages_event_id",
                        column: x => x.event_id,
                        principalSchema: "device_rental",
                        principalTable: "outbox_messages",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalSchema: "device_rental",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_claimed_lease",
                schema: "device_rental",
                table: "outbox_messages",
                columns: new[] { "status", "locked_until" },
                filter: "status = 'CLAIMED'");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending_due",
                schema: "device_rental",
                table: "outbox_messages",
                columns: new[] { "status", "available_at" },
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_recipient",
                schema: "device_rental",
                table: "notification_deliveries",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_notification_deliveries_event_dedupe",
                schema: "device_rental",
                table: "notification_deliveries",
                columns: new[] { "event_id", "dedupe_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_deliveries",
                schema: "device_rental");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_claimed_lease",
                schema: "device_rental",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending_due",
                schema: "device_rental",
                table: "outbox_messages");
        }
    }
}
