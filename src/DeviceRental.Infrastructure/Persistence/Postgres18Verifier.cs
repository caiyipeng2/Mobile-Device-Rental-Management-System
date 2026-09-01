using System.Globalization;
using Npgsql;

namespace DeviceRental.Infrastructure.Persistence;

public static class Postgres18Verifier
{
    public static async Task VerifyAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var openedHere = connection.State == System.Data.ConnectionState.Closed;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = new NpgsqlCommand("SHOW server_version_num;", connection);
            var rawVersion = Convert.ToString(
                await command.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
            if (!int.TryParse(rawVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var versionNumber) ||
                versionNumber / 10_000 != 18)
            {
                throw new InvalidOperationException(
                    $"Device Rental requires PostgreSQL 18; server_version_num was '{rawVersion}'.");
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
