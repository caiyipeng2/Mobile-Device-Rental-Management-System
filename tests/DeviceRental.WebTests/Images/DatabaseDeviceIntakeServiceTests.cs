using DeviceRental.Application.Devices;
using DeviceRental.Domain.Devices;
using DeviceRental.Web.Database;
using DeviceRental.Web.Demo;
using Xunit;

namespace DeviceRental.WebTests.Images;

public sealed class DatabaseDeviceIntakeServiceTests
{
    [Fact]
    [Trait("Category", "Web")]
    public async Task RegisterAsync_validates_stores_and_registers_the_same_image_reference()
    {
        var storage = new RecordingImageStorage();
        var registration = new RecordingRegistrationStore();
        var service = new DatabaseDeviceIntakeService(
            new DeviceImageUploadPolicy(new FixedImageDecoder()),
            registration,
            [storage]);

        await using var content = new MemoryStream(PngSignature, writable: false);
        var result = await service.RegisterAsync(
            "UPLOAD-WEB-001",
            "Pixel 10",
            "高端",
            content,
            new DemoCurrentUser("陈述", true, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(storage.Saved);
        Assert.NotNull(registration.Device);
        Assert.NotNull(registration.Image);
        Assert.Equal(storage.Saved!.Id, registration.Image!.Id);
        Assert.Equal(registration.Image.Id, registration.Device!.ImageId);
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task RegisterAsync_rejects_invalid_image_before_writing_storage()
    {
        var storage = new RecordingImageStorage();
        var registration = new RecordingRegistrationStore();
        var service = new DatabaseDeviceIntakeService(
            new DeviceImageUploadPolicy(new FixedImageDecoder()),
            registration,
            [storage]);

        await using var content = new MemoryStream([0x4d, 0x5a, 0x00, 0x01], writable: false);
        var result = await service.RegisterAsync(
            "UPLOAD-WEB-002",
            "Galaxy S26",
            "高端",
            content,
            new DemoCurrentUser("陈述", true, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(storage.Saved);
        Assert.Null(registration.Device);
    }

    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private sealed class FixedImageDecoder : IDeviceImageDecoder
    {
        public ValueTask<DeviceImageInspection> InspectAsync(
            Stream content,
            DeviceImageDecodeLimits limits,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new DeviceImageInspection(2, 2, 1));
    }

    private sealed class RecordingImageStorage : IDeviceImageStorage
    {
        public StoredDeviceImage? Saved { get; private set; }

        public async Task<StoredDeviceImage> SaveAsync(
            Guid imageId,
            ValidatedDeviceImage image,
            CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            await image.Content.CopyToAsync(copy, cancellationToken);
            Saved = new StoredDeviceImage(
                imageId,
                $"images/{imageId:N}.png",
                image.ContentType,
                copy.Length,
                image.PixelWidth,
                image.PixelHeight,
                image.Sha256Hex);
            return Saved;
        }

        public ValueTask<Stream> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRegistrationStore : IDeviceRegistrationStore
    {
        public Device? Device { get; private set; }

        public DeviceImageMetadata? Image { get; private set; }

        public Task<DeviceRegistrationStoreResult> RegisterAsync(
            Device device,
            DeviceImageMetadata image,
            CancellationToken cancellationToken = default)
        {
            Device = device;
            Image = image;
            return Task.FromResult(new DeviceRegistrationStoreResult(DeviceRegistrationStoreStatus.Succeeded));
        }
    }
}
