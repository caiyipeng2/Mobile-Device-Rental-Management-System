using DeviceRental.Application.Devices;
using DeviceRental.Domain.Devices;
using DeviceRental.Infrastructure.Devices;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using DeviceRental.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeviceRental.IntegrationTests.Devices;

[Collection(DeviceRental.IntegrationTests.DatabaseCollection.Name)]
public sealed class EfDeviceRegistrationStoreTests(PostgresTestEnvironment database)
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task RegisterAsync_saves_device_and_image_metadata_together()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var image = CreateImage();
        var device = new Device(Guid.NewGuid(), "UPLOAD-001", "Pixel 10", DeviceTier.High, image.Id);

        await using (var context = CreateContext())
        {
            var result = await new EfDeviceRegistrationStore(context)
                .RegisterAsync(device, image, cancellationToken);
            Assert.Equal(DeviceRegistrationStoreStatus.Succeeded, result.Status);
        }

        await using var verify = CreateContext();
        var persistedDevice = await verify.Devices.SingleAsync(value => value.Id == device.Id, cancellationToken);
        var persistedImage = await verify.DeviceImages.SingleAsync(value => value.Id == image.Id, cancellationToken);
        Assert.Equal(image.Id, persistedDevice.ImageId);
        Assert.Equal(image.StorageKey, persistedImage.StorageKey);
    }

    [Fact]
    [Trait("Category", "Database")]
    public async Task RegisterAsync_duplicate_asset_rolls_back_the_new_image_metadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await PrepareDatabaseAsync(cancellationToken);
        var firstImage = CreateImage();
        var firstDevice = new Device(Guid.NewGuid(), "UPLOAD-DUP", "Pixel 10", DeviceTier.High, firstImage.Id);
        await using (var context = CreateContext())
        {
            Assert.Equal(
                DeviceRegistrationStoreStatus.Succeeded,
                (await new EfDeviceRegistrationStore(context).RegisterAsync(firstDevice, firstImage, cancellationToken)).Status);
        }

        var secondImage = CreateImage();
        var secondDevice = new Device(Guid.NewGuid(), "UPLOAD-DUP", "Galaxy S26", DeviceTier.High, secondImage.Id);
        await using (var context = CreateContext())
        {
            var result = await new EfDeviceRegistrationStore(context)
                .RegisterAsync(secondDevice, secondImage, cancellationToken);
            Assert.Equal(DeviceRegistrationStoreStatus.DuplicateAssetNumber, result.Status);
        }

        await using var verify = CreateContext();
        Assert.False(await verify.DeviceImages.AnyAsync(value => value.Id == secondImage.Id, cancellationToken));
        Assert.Single(await verify.Devices.Where(value => value.AssetNumber == "UPLOAD-DUP").ToListAsync(cancellationToken));
    }

    private async Task PrepareDatabaseAsync(CancellationToken cancellationToken)
    {
        await DatabaseReset.ResetAsync(database, cancellationToken);
        await using var context = CreateContext();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private DeviceRentalDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DeviceRentalDbContext>()
            .UseNpgsql(database.MigrationConnectionString, options =>
            {
                options.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.GetName().Name);
                options.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
            })
            .Options);

    private static DeviceImageMetadata CreateImage() => new(
        Guid.NewGuid(),
        $"images/{Guid.NewGuid():N}.png",
        "image/png",
        128,
        2,
        2,
        new string('b', 64),
        DateTimeOffset.UtcNow);
}
