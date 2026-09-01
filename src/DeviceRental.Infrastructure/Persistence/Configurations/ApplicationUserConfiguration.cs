using DeviceRental.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceRental.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint(
                "ck_users_id_nonzero",
                "id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "ck_users_email_identity",
                "email = btrim(email) AND user_name = btrim(user_name) AND " +
                "normalized_email = btrim(normalized_email) AND normalized_user_name = btrim(normalized_user_name) AND " +
                "email <> '' AND normalized_email <> '' AND user_name = email AND " +
                "normalized_user_name = normalized_email");
            table.HasCheckConstraint(
                "ck_users_real_name_not_blank",
                "btrim(real_name) <> ''");
            table.HasCheckConstraint(
                "ck_users_authorization_version_positive",
                "authorization_version > 0");
            table.HasCheckConstraint(
                "ck_users_access_failed_count_nonnegative",
                "access_failed_count >= 0");
            table.HasCheckConstraint(
                "ck_users_email_verification_tuple",
                "(email_confirmed AND email_verified_at IS NOT NULL) OR " +
                "(NOT email_confirmed AND email_verified_at IS NULL)");
            table.HasCheckConstraint(
                "ck_users_timestamps",
                "updated_at >= created_at AND " +
                "(email_verified_at IS NULL OR email_verified_at >= created_at)");
        });

        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.UserName).HasColumnName("user_name").HasMaxLength(256).IsRequired();
        builder.Property(user => user.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(256).IsRequired();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256).IsRequired();
        builder.Property(user => user.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash");
        builder.Property(user => user.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(user => user.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.Property(user => user.PhoneNumber).HasColumnName("phone_number");
        builder.Property(user => user.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(user => user.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(user => user.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(user => user.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(user => user.AccessFailedCount).HasColumnName("access_failed_count");
        builder.Property(user => user.RealName).HasColumnName("real_name").HasMaxLength(200).IsRequired();
        builder.Property(user => user.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(user => user.AuthorizationVersion).HasColumnName("authorization_version").HasDefaultValue(1L).IsRequired();
        builder.Property(user => user.EmailVerifiedAt).HasColumnName("email_verified_at").HasColumnType("timestamp with time zone");
        builder.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(user => user.NormalizedUserName)
            .HasDatabaseName("ux_users_normalized_user_name")
            .IsUnique();
        builder.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("ux_users_normalized_email")
            .IsUnique();
    }
}

public sealed class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("roles", table => table.HasCheckConstraint(
            "ck_roles_id_nonzero",
            "id <> '00000000-0000-0000-0000-000000000000'::uuid"));
        builder.ToTable("roles", table => table.HasCheckConstraint(
            "ck_roles_approved_name",
            "name = btrim(name) AND normalized_name = btrim(normalized_name) AND " +
            "name <> '' AND name = normalized_name AND normalized_name IN ('USER', 'TEST_ADMIN')"));
        builder.Property(role => role.Id).HasColumnName("id");
        builder.Property(role => role.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        builder.Property(role => role.NormalizedName).HasColumnName("normalized_name").HasMaxLength(256).IsRequired();
        builder.Property(role => role.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.HasIndex(role => role.NormalizedName)
            .HasDatabaseName("ux_roles_normalized_name")
            .IsUnique();
    }
}

public sealed class IdentityUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("user_roles");
        builder.Property(link => link.UserId).HasColumnName("user_id");
        builder.Property(link => link.RoleId).HasColumnName("role_id");
        builder.HasKey(link => new { link.UserId, link.RoleId });
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(link => link.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey(link => link.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claims");
        builder.Property(claim => claim.Id).HasColumnName("id");
        builder.Property(claim => claim.UserId).HasColumnName("user_id");
        builder.Property(claim => claim.ClaimType).HasColumnName("claim_type");
        builder.Property(claim => claim.ClaimValue).HasColumnName("claim_value");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(claim => claim.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_logins");
        builder.Property(login => login.LoginProvider).HasColumnName("login_provider");
        builder.Property(login => login.ProviderKey).HasColumnName("provider_key");
        builder.Property(login => login.ProviderDisplayName).HasColumnName("provider_display_name");
        builder.Property(login => login.UserId).HasColumnName("user_id");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.LoginProvider).HasColumnName("login_provider");
        builder.Property(token => token.Name).HasColumnName("name");
        builder.Property(token => token.Value).HasColumnName("value");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("role_claims");
        builder.Property(claim => claim.Id).HasColumnName("id");
        builder.Property(claim => claim.RoleId).HasColumnName("role_id");
        builder.Property(claim => claim.ClaimType).HasColumnName("claim_type");
        builder.Property(claim => claim.ClaimValue).HasColumnName("claim_value");
        builder.HasOne<IdentityRole<Guid>>()
            .WithMany()
            .HasForeignKey(claim => claim.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
