using Npgsql;

namespace DeviceRental.Testing;

public static class DatabaseReset
{
    public static async Task ResetAsync(
        PostgresTestEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        NpgsqlConnection.ClearAllPools();

        await using var connection = environment.CreateMigrationConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            DROP SCHEMA IF EXISTS device_rental CASCADE;
            DROP TABLE IF EXISTS public."__EFMigrationsHistory";
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task GrantApplicationAccessAsync(
        PostgresTestEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);

        await using var connection = environment.CreateMigrationConnection();
        await connection.OpenAsync(cancellationToken);
        var role = QuoteIdentifier(environment.ApplicationRoleName);
        var sql = $"""
            GRANT USAGE ON SCHEMA device_rental TO {role};
            GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA device_rental TO {role};
            GRANT DELETE ON TABLE
                device_rental.user_claims,
                device_rental.user_logins,
                device_rental.user_roles,
                device_rental.user_tokens,
                device_rental.role_claims
            TO {role};
            GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA device_rental TO {role};
            REVOKE UPDATE ON TABLE device_rental.audit_events FROM {role};
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
