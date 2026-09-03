using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeviceRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_images",
                schema: "device_rental",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    byte_length = table.Column<long>(type: "bigint", nullable: false),
                    pixel_width = table.Column<int>(type: "integer", nullable: false),
                    pixel_height = table.Column<int>(type: "integer", nullable: false),
                    sha256_hex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_images", x => x.id);
                    table.CheckConstraint("ck_device_images_content_type", "content_type IN ('image/jpeg', 'image/png', 'image/webp')");
                    table.CheckConstraint("ck_device_images_dimensions", "byte_length > 0 AND byte_length <= 5242880 AND pixel_width > 0 AND pixel_height > 0 AND pixel_width <= 4096 AND pixel_height <= 4096 AND (pixel_width::bigint * pixel_height::bigint) <= 16000000");
                    table.CheckConstraint("ck_device_images_id_nonzero", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_device_images_sha256", "sha256_hex ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_device_images_storage_key", "storage_key LIKE 'images/%' AND btrim(storage_key) <> ''");
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_images_sha256",
                schema: "device_rental",
                table: "device_images",
                column: "sha256_hex");

            migrationBuilder.CreateIndex(
                name: "ux_device_images_storage_key",
                schema: "device_rental",
                table: "device_images",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_images",
                schema: "device_rental");
        }
    }
}
