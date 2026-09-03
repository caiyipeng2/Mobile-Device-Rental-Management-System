using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoanPolicyVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_policy_versions",
                schema: "device_rental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    effective_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_policy_versions", x => x.id);
                    table.CheckConstraint("ck_loan_policy_versions_actor_nonzero", "changed_by_user_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_loan_policy_versions_duration", "duration_minutes BETWEEN 60 AND 10080");
                    table.CheckConstraint("ck_loan_policy_versions_id_nonzero", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_loan_policy_versions_reason", "btrim(reason) <> ''");
                    table.CheckConstraint("ck_loan_policy_versions_version_positive", "version_number > 0");
                    table.ForeignKey(
                        name: "FK_loan_policy_versions_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalSchema: "device_rental",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loan_policy_versions_changed_by_user_id",
                schema: "device_rental",
                table: "loan_policy_versions",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_policy_versions_effective",
                schema: "device_rental",
                table: "loan_policy_versions",
                columns: new[] { "effective_at_utc", "version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_loan_policy_versions_version",
                schema: "device_rental",
                table: "loan_policy_versions",
                column: "version_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_policy_versions",
                schema: "device_rental");
        }
    }
}
