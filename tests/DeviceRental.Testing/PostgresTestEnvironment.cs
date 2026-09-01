using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DeviceRental.Testing;

public sealed class PostgresTestEnvironment : IAsyncLifetime
{
    private const string AdminEnvironmentVariable = "DEVICERENTAL_TEST_POSTGRES_ADMIN";
    private const string ResourcePrefix = "dr_test_";

    private PostgreSqlContainer? _container;
    private NpgsqlConnectionStringBuilder? _adminConnection;
    private string? _migrationPassword;
    private string? _applicationPassword;

    public string DatabaseName { get; private set; } = string.Empty;

    public string MigrationRoleName { get; private set; } = string.Empty;

    public string ApplicationRoleName { get; private set; } = string.Empty;

    public string MigrationConnectionString => BuildDatabaseConnectionString(
        MigrationRoleName,
        _migrationPassword);

    public string ApplicationConnectionString => BuildDatabaseConnectionString(
        ApplicationRoleName,
        _applicationPassword);

    public async ValueTask InitializeAsync()
    {
        var configuredAdmin = Environment.GetEnvironmentVariable(AdminEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredAdmin))
        {
            _adminConnection = ParseAdminConnectionString(configuredAdmin);
        }
        else
        {
            await StartContainerWhenDockerIsAvailableAsync();
        }

        ArgumentNullException.ThrowIfNull(_adminConnection);
        try
        {
            await RequirePostgreSql18Async(_adminConnection.ConnectionString);
        }
        catch
        {
            await DisposeContainerAsync();
            throw;
        }

        var suffix = Guid.NewGuid().ToString("N")[..12];
        DatabaseName = $"{ResourcePrefix}{suffix}";
        MigrationRoleName = $"dr_migrator_{suffix}";
        ApplicationRoleName = $"dr_app_{suffix}";
        _migrationPassword = CreatePassword();
        _applicationPassword = CreatePassword();

        try
        {
            await CreateIsolatedResourcesAsync();
            await RequirePostgreSql18Async(MigrationConnectionString);
        }
        catch (Exception exception)
        {
            var cleanupFailure = await TryCleanupIsolatedResourcesAsync();
            await DisposeContainerAsync();
            throw new InvalidOperationException(
                "PostgreSQL 18 was reachable, but the test fixture could not create its " +
                "isolated database and least-privilege roles. The admin connection must " +
                "have CREATEDB and CREATEROLE privileges.",
                cleanupFailure is null
                    ? exception
                    : new AggregateException(exception, cleanupFailure));
        }
    }

    public async ValueTask DisposeAsync()
    {
        var cleanupFailure = await TryCleanupIsolatedResourcesAsync();
        await DisposeContainerAsync();
        if (cleanupFailure is not null)
        {
            throw new InvalidOperationException(
                $"Failed to remove isolated PostgreSQL test resources for {DatabaseName}.",
                cleanupFailure);
        }
    }

    public NpgsqlConnection CreateMigrationConnection() =>
        new(MigrationConnectionString);

    public NpgsqlConnection CreateApplicationConnection() =>
        new(ApplicationConnectionString);

    public static async Task RequirePostgreSql18Async(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SHOW server_version_num;", connection);
        var rawVersion = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (!int.TryParse(rawVersion, out var versionNumber) || versionNumber / 10_000 != 18)
        {
            throw new InvalidOperationException(
                $"Database integration tests require PostgreSQL 18; server_version_num was '{rawVersion}'.");
        }
    }

    private static NpgsqlConnectionStringBuilder ParseAdminConnectionString(string value)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(value)
            {
                Pooling = false,
                IncludeErrorDetail = true,
            };
            if (string.IsNullOrWhiteSpace(builder.Host) || string.IsNullOrWhiteSpace(builder.Username))
            {
                throw new ArgumentException("Host and Username are required.", nameof(value));
            }

            return builder;
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            throw new InvalidOperationException(
                $"{AdminEnvironmentVariable} is not a valid PostgreSQL admin connection string.",
                exception);
        }
    }

    private async Task StartContainerWhenDockerIsAvailableAsync()
    {
        var dockerProbe = await ProbeDockerAsync();
        if (!dockerProbe.Available)
        {
            throw new InvalidOperationException(
                "PostgreSQL 18 integration tests cannot start. Set " +
                $"{AdminEnvironmentVariable} to a PG18 admin connection string, or install " +
                $"and start Docker. Docker probe: {dockerProbe.Detail}");
        }

        var image = ReadPinnedPostgresImage();
        try
        {
            _container = new PostgreSqlBuilder(image)
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword(CreatePassword())
                // The fixture disposes this container directly; disabling Ryuk avoids an
                // undeclared auxiliary image outside eng/container-images.json.
                .WithCleanUp(false)
                .Build();
            await _container.StartAsync();
            _adminConnection = ParseAdminConnectionString(_container.GetConnectionString());
        }
        catch (Exception exception)
        {
            await DisposeContainerAsync();
            throw new InvalidOperationException(
                "Docker is available, but the digest-pinned PostgreSQL 18 test container " +
                "could not start. Check Docker image access and daemon logs.",
                exception);
        }
    }

    private async Task CreateIsolatedResourcesAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnection!.ConnectionString);
        await connection.OpenAsync();

        await ExecuteAsync(
            connection,
            $"CREATE ROLE {QuoteIdentifier(MigrationRoleName)} LOGIN PASSWORD " +
            $"{QuoteLiteral(_migrationPassword!)} NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;");
        await ExecuteAsync(
            connection,
            $"CREATE ROLE {QuoteIdentifier(ApplicationRoleName)} LOGIN PASSWORD " +
            $"{QuoteLiteral(_applicationPassword!)} NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;");
        await ExecuteAsync(
            connection,
            $"CREATE DATABASE {QuoteIdentifier(DatabaseName)};");
        await ExecuteAsync(
            connection,
            $"REVOKE CONNECT ON DATABASE {QuoteIdentifier(DatabaseName)} FROM PUBLIC;");
        await ExecuteAsync(
            connection,
            $"GRANT CONNECT, CREATE, TEMPORARY ON DATABASE {QuoteIdentifier(DatabaseName)} " +
            $"TO {QuoteIdentifier(MigrationRoleName)};");
        await ExecuteAsync(
            connection,
            $"GRANT CONNECT ON DATABASE {QuoteIdentifier(DatabaseName)} TO {QuoteIdentifier(ApplicationRoleName)};");
    }

    private async Task<Exception?> TryCleanupIsolatedResourcesAsync()
    {
        if (_adminConnection is null || string.IsNullOrWhiteSpace(DatabaseName))
        {
            return null;
        }

        try
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_adminConnection.ConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                $"DROP DATABASE IF EXISTS {QuoteIdentifier(DatabaseName)} WITH (FORCE);");
            await ExecuteAsync(
                connection,
                $"DROP ROLE IF EXISTS {QuoteIdentifier(ApplicationRoleName)};");
            await ExecuteAsync(
                connection,
                $"DROP ROLE IF EXISTS {QuoteIdentifier(MigrationRoleName)};");
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private string BuildDatabaseConnectionString(string roleName, string? password)
    {
        if (_adminConnection is null || string.IsNullOrWhiteSpace(roleName) || password is null)
        {
            throw new InvalidOperationException("The PostgreSQL test fixture is not initialized.");
        }

        return new NpgsqlConnectionStringBuilder(_adminConnection.ConnectionString)
        {
            Database = DatabaseName,
            Username = roleName,
            Password = password,
            Pooling = false,
            IncludeErrorDetail = true,
            ApplicationName = $"device-rental-tests-{roleName}",
        }.ConnectionString;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        params NpgsqlParameter[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string QuoteLiteral(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string CreatePassword() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private static async Task<(bool Available, string Detail)> ProbeDockerAsync()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format {{.ServerVersion}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return (false, "the docker process could not be created");
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                return (false, "docker info timed out after 10 seconds");
            }

            var detail = process.ExitCode == 0
                ? (await process.StandardOutput.ReadToEndAsync()).Trim()
                : (await process.StandardError.ReadToEndAsync()).Trim();
            return (process.ExitCode == 0, string.IsNullOrWhiteSpace(detail) ? "no diagnostic output" : detail);
        }
        catch (Win32Exception)
        {
            return (false, "docker CLI was not found on PATH");
        }
    }

    private static string ReadPinnedPostgresImage()
    {
        var manifestPath = FindRepositoryFile(Path.Combine("eng", "container-images.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var image = document.RootElement
            .GetProperty("images")
            .GetProperty("postgres")
            .GetString();
        if (string.IsNullOrWhiteSpace(image) ||
            !image.StartsWith("postgres:18", StringComparison.Ordinal) ||
            !image.Contains("@sha256:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The PostgreSQL test image in {manifestPath} must pin PostgreSQL 18 by digest.");
        }

        return image;
    }

    private static string FindRepositoryFile(string relativePath)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Cannot locate {relativePath}; run database tests from a repository checkout.");
    }

    private async Task DisposeContainerAsync()
    {
        if (_container is null)
        {
            return;
        }

        await _container.DisposeAsync();
        _container = null;
    }
}
