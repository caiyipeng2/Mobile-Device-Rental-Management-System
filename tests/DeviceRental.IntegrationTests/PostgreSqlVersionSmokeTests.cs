using Npgsql;
using Xunit;

namespace DeviceRental.IntegrationTests;

public sealed class PostgreSqlVersionSmokeTests
{
    [Fact]
    public async Task ConfiguredServer_IsPostgreSql18()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "DEVICERENTAL_TEST_POSTGRES_ADMIN");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "DEVICERENTAL_TEST_POSTGRES_ADMIN is required for database integration tests.");

        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(connectionString);
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
