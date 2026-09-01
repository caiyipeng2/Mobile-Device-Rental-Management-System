using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace DeviceRental.IntegrationTests.Migrations;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class MigrationSmokeTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "NFR-REL-001")]
    public async Task LatestMigration_AppliesToEmptyPostgreSql18()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PostgresTestEnvironment.RequirePostgreSql18Async(
            database.MigrationConnectionString,
            cancellationToken);
        await DatabaseReset.ResetAsync(database, cancellationToken);

        await using var context = InfrastructureDbContextFactory.Create(
            database.MigrationConnectionString);
        var migrations = context.Database.GetMigrations().ToArray();
        Assert.Collection(
            migrations,
            migration => Assert.EndsWith("_IdentityAndAccessPolicy", migration, StringComparison.Ordinal),
            migration => Assert.EndsWith("_AuditAndOutbox", migration, StringComparison.Ordinal));

        var modelTypes = context.Model.GetEntityTypes()
            .Select(entity => entity.ClrType.FullName)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("DeviceRental.Infrastructure.Identity.ApplicationUser", modelTypes);
        Assert.Contains("DeviceRental.Infrastructure.Persistence.Records.AuditEventRecord", modelTypes);
        Assert.Contains("DeviceRental.Infrastructure.Persistence.Records.OutboxMessageRecord", modelTypes);
        Assert.DoesNotContain("DeviceRental.Domain.Auditing.AuditEvent", modelTypes);
        Assert.DoesNotContain("DeviceRental.Domain.Notifications.OutboxMessage", modelTypes);
        Assert.DoesNotContain("DeviceRental.Domain.Notifications.EncryptedPayload", modelTypes);

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[0], cancellationToken);
        var expectedIdentityTables = new[]
        {
            "__EFMigrationsHistory",
            "role_claims",
            "roles",
            "user_claims",
            "user_logins",
            "user_roles",
            "user_tokens",
            "users",
        };
        Assert.Equal(expectedIdentityTables, await ReadSchemaTablesAsync(cancellationToken));

        await migrator.MigrateAsync(migrations[1], cancellationToken);
        var latestTables = await ReadSchemaTablesAsync(cancellationToken);
        Assert.Contains("audit_events", latestTables);
        Assert.Contains("outbox_messages", latestTables);

        await migrator.MigrateAsync(migrations[0], cancellationToken);
        Assert.Equal(expectedIdentityTables, await ReadSchemaTablesAsync(cancellationToken));

        await migrator.MigrateAsync(migrations[1], cancellationToken);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
    }

    private async Task<string[]> ReadSchemaTablesAsync(CancellationToken cancellationToken)
    {
        await using var connection = database.CreateMigrationConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'device_rental'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return [.. tables];
    }
}
