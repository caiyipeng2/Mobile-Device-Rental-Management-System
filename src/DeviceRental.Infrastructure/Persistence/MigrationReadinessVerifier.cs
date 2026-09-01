using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeviceRental.Infrastructure.Persistence;

public static class MigrationReadinessVerifier
{
    public static async Task VerifyAsync(
        DbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Database.GetDbConnection() is not NpgsqlConnection connection)
        {
            throw new InvalidOperationException(
                "Device Rental persistence must use the Npgsql PostgreSQL provider.");
        }

        await Postgres18Verifier.VerifyAsync(connection, cancellationToken);
        var pendingMigrations = await context.Database
            .GetPendingMigrationsAsync(cancellationToken);
        var pending = pendingMigrations.ToArray();
        if (pending.Length != 0)
        {
            throw new InvalidOperationException(
                $"Database schema is not ready. Pending migrations: {string.Join(", ", pending)}.");
        }
    }
}
