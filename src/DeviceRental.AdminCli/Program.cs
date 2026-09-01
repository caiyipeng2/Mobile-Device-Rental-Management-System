using DeviceRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

if (args.Length != 1 || !string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: DeviceRental.AdminCli migrate");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("DEVICERENTAL_MIGRATION_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "DEVICERENTAL_MIGRATION_CONNECTION must contain the PostgreSQL 18 migrator connection string.");
    return 2;
}

await using (var connection = new NpgsqlConnection(connectionString))
{
    await Postgres18Verifier.VerifyAsync(connection);
}

var options = new DbContextOptionsBuilder<DeviceRentalDbContext>()
    .UseNpgsql(connectionString, postgres =>
    {
        postgres.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.FullName);
        postgres.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            DeviceRentalDbContext.SchemaName);
    })
    .Options;
await using var database = new DeviceRentalDbContext(options);
await database.Database.MigrateAsync();
await MigrationReadinessVerifier.VerifyAsync(database);

Console.WriteLine("Database migrations applied; PostgreSQL 18 schema is ready.");
return 0;
