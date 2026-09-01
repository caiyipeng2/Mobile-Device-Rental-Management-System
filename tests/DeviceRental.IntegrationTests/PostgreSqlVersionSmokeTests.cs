using DeviceRental.Testing;
using Npgsql;
using Xunit;

namespace DeviceRental.IntegrationTests;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class PostgreSqlVersionSmokeTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "NFR-REL-001")]
    public async Task ConfiguredServer_IsPostgreSql18()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = database.CreateMigrationConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand("SHOW server_version_num;", connection);
        var rawVersion = Assert.IsType<string>(
            await command.ExecuteScalarAsync(cancellationToken));
        Assert.True(
            int.TryParse(rawVersion, out var versionNumber),
            $"PostgreSQL returned an invalid server_version_num: {rawVersion}");
        Assert.Equal(18, versionNumber / 10_000);
    }
}
