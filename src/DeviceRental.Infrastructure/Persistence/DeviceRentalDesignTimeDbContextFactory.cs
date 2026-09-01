using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeviceRental.Infrastructure.Persistence;

public sealed class DeviceRentalDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<DeviceRentalDbContext>
{
    public DeviceRentalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVICERENTAL_MIGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DEVICERENTAL_MIGRATION_CONNECTION must contain the PostgreSQL 18 migrator connection string.");
        }
        var options = new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(connectionString, postgres =>
            {
                postgres.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.FullName);
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options;

        return new DeviceRentalDbContext(options);
    }
}
