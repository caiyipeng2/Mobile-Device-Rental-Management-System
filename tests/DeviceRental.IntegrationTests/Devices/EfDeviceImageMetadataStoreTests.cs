using DeviceRental.Application.Devices;
using DeviceRental.Domain.Devices;
using DeviceRental.Infrastructure.Devices;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeviceRental.IntegrationTests.Devices;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class EfDeviceImageMetadataStoreTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task Save_then_find_round_trips_image_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        var metadata = new DeviceImageMetadata(
            Guid.NewGuid(),
            $"images/{Guid.NewGuid():N}.png",
            "image/png",
            128,
            2,
            2,
            new string('a', 64),
            DateTimeOffset.UtcNow);

        await using (var writeContext = CreateContext())
        {
            var store = new EfDeviceImageMetadataStore(writeContext);
            await store.SaveAsync(metadata, cancellationToken);
        }

        await using var readContext = CreateContext();
        var found = await new EfDeviceImageMetadataStore(readContext)
            .FindAsync(metadata.Id, cancellationToken);

        Assert.NotNull(found);
        Assert.Equal(metadata.StorageKey, found!.StorageKey);
        Assert.Equal(metadata.Sha256Hex, found.Sha256Hex);
        Assert.Equal(metadata.PixelWidth, found.PixelWidth);
    }

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);
}
