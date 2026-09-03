using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class DeviceImageMetadataRecordConfiguration : IEntityTypeConfiguration<DeviceImageMetadataRecord>
{
    public void Configure(EntityTypeBuilder<DeviceImageMetadataRecord> builder)
    {
        builder.ToTable("device_images", table =>
        {
            table.HasCheckConstraint(
                "ck_device_images_id_nonzero",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_device_images_storage_key",
                "storage_key LIKE 'images/%' AND btrim(storage_key) <> ''");
            table.HasCheckConstraint(
                "ck_device_images_content_type",
                "content_type IN ('image/jpeg', 'image/png', 'image/webp')");
            table.HasCheckConstraint(
                "ck_device_images_dimensions",
                "byte_length > 0 AND byte_length <= 5242880 AND pixel_width > 0 AND pixel_height > 0 AND pixel_width <= 4096 AND pixel_height <= 4096 AND (pixel_width::bigint * pixel_height::bigint) <= 16000000");
            table.HasCheckConstraint(
                "ck_device_images_sha256",
                "sha256_hex ~ '^[0-9a-f]{64}$'");
        });

        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).HasColumnName("id");
        builder.Property(image => image.StorageKey).HasColumnName("storage_key").HasMaxLength(300).IsRequired();
        builder.Property(image => image.ContentType).HasColumnName("content_type").HasMaxLength(32).IsRequired();
        builder.Property(image => image.ByteLength).HasColumnName("byte_length").IsRequired();
        builder.Property(image => image.PixelWidth).HasColumnName("pixel_width").IsRequired();
        builder.Property(image => image.PixelHeight).HasColumnName("pixel_height").IsRequired();
        builder.Property(image => image.Sha256Hex).HasColumnName("sha256_hex").HasMaxLength(64).IsRequired();
        builder.Property(image => image.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(image => image.StorageKey).HasDatabaseName("ux_device_images_storage_key").IsUnique();
        builder.HasIndex(image => image.Sha256Hex).HasDatabaseName("ix_device_images_sha256");
    }
}
