using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DeviceRental.Infrastructure.Persistence;

public sealed class DeviceRentalDbContext(
    DbContextOptions<DeviceRentalDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public const string SchemaName = "device_rental";

    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();

    public DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();

    public override int SaveChanges()
    {
        RejectAuditHistoryMutations();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectAuditHistoryMutations();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RejectAuditHistoryMutations();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RejectAuditHistoryMutations();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeviceRentalDbContext).Assembly);
    }

    private void RejectAuditHistoryMutations()
    {
        var mutation = ChangeTracker.Entries<AuditEventRecord>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (mutation is not null)
        {
            throw new InvalidOperationException("Audit history is append-only and cannot be updated or deleted.");
        }
    }
}
