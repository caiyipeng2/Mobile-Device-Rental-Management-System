using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class LoanRecordConfiguration : IEntityTypeConfiguration<LoanRecord>
{
    public void Configure(EntityTypeBuilder<LoanRecord> builder)
    {
        builder.ToTable("loans", table =>
        {
            table.HasCheckConstraint(
                "ck_loans_id_nonzero",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_loans_parties_nonzero",
                "device_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "borrower_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_loans_time_order",
                "due_at > borrowed_at AND (returned_at IS NULL OR returned_at >= borrowed_at)");
            table.HasCheckConstraint(
                "ck_loans_return_tuple",
                "(returned_at IS NULL AND returned_by_user_id IS NULL AND return_kind IS NULL AND return_reason IS NULL) OR " +
                "(returned_at IS NOT NULL AND returned_by_user_id IS NOT NULL AND returned_by_user_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "return_kind IN ('SELF', 'FORCED') AND " +
                "((return_kind = 'SELF' AND returned_by_user_id = borrower_id AND return_reason IS NULL) OR " +
                "(return_kind = 'FORCED' AND return_reason IS NOT NULL AND btrim(return_reason) <> '')))");
            table.HasCheckConstraint("ck_loans_version_positive", "version > 0");
        });

        builder.HasKey(loan => loan.Id);
        builder.Property(loan => loan.Id).HasColumnName("id");
        builder.Property(loan => loan.DeviceId).HasColumnName("device_id");
        builder.Property(loan => loan.BorrowerId).HasColumnName("borrower_id");
        builder.Property(loan => loan.BorrowedAt).HasColumnName("borrowed_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(loan => loan.DueAt).HasColumnName("due_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(loan => loan.PolicyVersionId).HasColumnName("policy_version_id");
        builder.Property(loan => loan.ReturnedAt).HasColumnName("returned_at").HasColumnType("timestamp with time zone");
        builder.Property(loan => loan.ReturnedByUserId).HasColumnName("returned_by_user_id");
        builder.Property(loan => loan.ReturnKind).HasColumnName("return_kind").HasMaxLength(16);
        builder.Property(loan => loan.ReturnReason).HasColumnName("return_reason").HasMaxLength(1000);
        builder.Property(loan => loan.Version).HasColumnName("version").HasDefaultValue(1L).IsRequired();

        builder.HasOne<DeviceRecord>()
            .WithMany()
            .HasForeignKey(loan => loan.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(loan => loan.BorrowerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(loan => loan.ReturnedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(loan => loan.DeviceId)
            .HasDatabaseName("ux_loans_open_device")
            .IsUnique()
            .HasFilter("returned_at IS NULL");
        builder.HasIndex(loan => new { loan.BorrowerId, loan.BorrowedAt })
            .HasDatabaseName("ix_loans_borrower_borrowed_at");
        builder.HasIndex(loan => new { loan.DueAt, loan.ReturnedAt })
            .HasDatabaseName("ix_loans_due_returned");
    }
}
