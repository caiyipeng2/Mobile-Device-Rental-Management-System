using DeviceRental.Testing;
using Xunit;

namespace DeviceRental.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresTestEnvironment>
{
    public const string Name = "PostgreSQL 18 database";
}
