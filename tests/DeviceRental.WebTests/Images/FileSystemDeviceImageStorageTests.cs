using System.Security.Cryptography;
using DeviceRental.Application.Devices;
using DeviceRental.Infrastructure.Images;
using Xunit;

namespace DeviceRental.WebTests.Images;

public sealed class FileSystemDeviceImageStorageTests
{
    [Fact]
    [Trait("Category", "Web")]
    public async Task Save_then_open_round_trips_bytes_under_a_private_object_key()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var bytes = new byte[] { 1, 2, 3, 4 };
            await using var source = new MemoryStream(bytes, writable: false);
            var image = new ValidatedDeviceImage(
                source,
                "image/png",
                bytes.Length,
                2,
                2,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

            var storage = new FileSystemDeviceImageStorage(root);
            var saved = await storage.SaveAsync(Guid.NewGuid(), image, TestContext.Current.CancellationToken);

            Assert.StartsWith("images/", saved.StorageKey, StringComparison.Ordinal);
            Assert.DoesNotContain("..", saved.StorageKey, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, saved.StorageKey.Replace('/', Path.DirectorySeparatorChar))));

            await using var opened = await storage.OpenReadAsync(saved.StorageKey, TestContext.Current.CancellationToken);
            using var copy = new MemoryStream();
            await opened.CopyToAsync(copy, TestContext.Current.CancellationToken);
            Assert.Equal(bytes, copy.ToArray());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task Open_read_rejects_path_traversal_keys()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var storage = new FileSystemDeviceImageStorage(root);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                storage.OpenReadAsync("../outside.bin", TestContext.Current.CancellationToken).AsTask());
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "device-rental-image-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
