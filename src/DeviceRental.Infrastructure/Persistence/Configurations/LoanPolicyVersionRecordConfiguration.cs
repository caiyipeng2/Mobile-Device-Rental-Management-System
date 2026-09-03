using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class LoanPolicyVersionRecordConfiguration : IEntityTypeConfiguration<LoanPolicyVersionRecord>
{
    public void Configure(EntityTypeBuilder<LoanPolicyVersionRecord> builder)
    {
        builder.ToTable("loan_policy_versions", table =>
        {
            table.HasCheckConstraint(
                "ck_loan_policy_versions_id_nonzero",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_loan_policy_versions_version_positive",
                "version_number > 0");
            table.HasCheckConstraint(
                "ck_loan_policy_versions_duration",
                "duration_minutes BETWEEN 60 AND 10080");
            table.HasCheckConstraint(
                "ck_loan_policy_versions_actor_nonzero",
                "changed_by_user_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_loan_policy_versions_reason",
                "btrim(reason) <> ''");
        });

        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Id).HasColumnName("id");
        builder.Property(policy => policy.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(policy => policy.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        builder.Property(policy => policy.EffectiveAtUtc).HasColumnName("effective_at_utc").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.ChangedByUserId).HasColumnName("changed_by_user_id").IsRequired();
        builder.Property(policy => policy.Reason).HasColumnName("reason").HasMaxLength(1000).IsRequired();
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(policy => policy.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(policy => policy.VersionNumber)
            .HasDatabaseName("ux_loan_policy_versions_version")
            .IsUnique();
        builder.HasIndex(policy => new { policy.EffectiveAtUtc, policy.VersionNumber })
            .HasDatabaseName("ix_loan_policy_versions_effective");
    }
}
