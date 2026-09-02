using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class DeviceRecordConfiguration : IEntityTypeConfiguration<DeviceRecord>
{
    public void Configure(EntityTypeBuilder<DeviceRecord> builder)
    {
        builder.ToTable("devices", table =>
        {
            table.HasCheckConstraint(
                "ck_devices_id_nonzero",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_devices_required_text",
                "btrim(asset_number) <> '' AND btrim(model_name) <> ''");
            table.HasCheckConstraint(
                "ck_devices_tier",
                "tier IN ('LOW', 'MID', 'HIGH')");
            table.HasCheckConstraint(
                "ck_devices_manual_state",
                "manual_state IN ('NORMAL', 'TEMPORARILY_DISABLED')");
            table.HasCheckConstraint(
                "ck_devices_unavailable_reason",
                "(manual_state = 'NORMAL' AND temporary_unavailable_reason IS NULL) OR " +
                "(manual_state = 'TEMPORARILY_DISABLED' AND temporary_unavailable_reason IS NOT NULL AND btrim(temporary_unavailable_reason) <> '')");
            table.HasCheckConstraint(
                "ck_devices_version_positive",
                "version > 0");
            table.HasCheckConstraint(
                "ck_devices_timestamps",
                "updated_at >= created_at");
        });

        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).HasColumnName("id");
        builder.Property(device => device.AssetNumber).HasColumnName("asset_number").HasMaxLength(64).IsRequired();
        builder.Property(device => device.ModelName).HasColumnName("model_name").HasMaxLength(200).IsRequired();
        builder.Property(device => device.Tier).HasColumnName("tier").HasMaxLength(8).IsRequired();
        builder.Property(device => device.ImageId).HasColumnName("image_id").IsRequired();
        builder.Property(device => device.ManualState).HasColumnName("manual_state").HasMaxLength(32).IsRequired();
        builder.Property(device => device.TemporaryUnavailableReason).HasColumnName("temporary_unavailable_reason").HasMaxLength(500);
        builder.Property(device => device.IsArchived).HasColumnName("is_archived").HasDefaultValue(false).IsRequired();
        builder.Property(device => device.Version).HasColumnName("version").HasDefaultValue(1L).IsRequired();
        builder.Property(device => device.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(device => device.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(device => device.AssetNumber).HasDatabaseName("ux_devices_asset_number").IsUnique();
        builder.HasIndex(device => new { device.IsArchived, device.Tier }).HasDatabaseName("ix_devices_archive_tier");
        builder.HasIndex(device => device.ManualState).HasDatabaseName("ix_devices_manual_state");
    }
}
