using System.Reflection;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DeviceRental.IntegrationTests.Database;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class PostgreSqlRuntimeVerifierTests
{
    [Fact]
    [Trait("Category", "Database")]
    [Trait("Requirement", "NFR-REL-001")]
    public void Infrastructure_ExposesSharedPostgreSql18AndMigrationReadinessVerifiers()
    {
        var assembly = InfrastructureDbContextFactory.LoadInfrastructureAssembly();
        AssertPublicTaskMethod(
            assembly,
            "DeviceRental.Infrastructure.Persistence.Postgres18Verifier",
            typeof(NpgsqlConnection),
            typeof(CancellationToken));
        AssertPublicTaskMethod(
            assembly,
            "DeviceRental.Infrastructure.Persistence.MigrationReadinessVerifier",
            typeof(DbContext),
            typeof(CancellationToken));
    }

    private static void AssertPublicTaskMethod(
        Assembly assembly,
        string typeName,
        params Type[] parameterTypes)
    {
        var type = assembly.GetType(typeName, throwOnError: false);
        Assert.NotNull(type);
        Assert.True(type.IsPublic, $"{typeName} must be public so all composition roots share it.");

        var method = type.GetMethod(
            "VerifyAsync",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Assert.NotNull(method);
        Assert.True(
            typeof(Task).IsAssignableFrom(method.ReturnType),
            $"{typeName}.VerifyAsync must return Task or Task<T>.");
    }
}
