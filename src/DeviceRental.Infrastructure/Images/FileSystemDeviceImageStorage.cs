using DeviceRental.Application.Devices;

namespace DeviceRental.Infrastructure.Images;

/// <summary>
/// Stores immutable device images below a configured private root. The key is generated from a
/// server-side UUID and the validated content type; callers never choose a filesystem path.
/// </summary>
public sealed class FileSystemDeviceImageStorage : IDeviceImageStorage
{
    private readonly string _rootPath;

    public FileSystemDeviceImageStorage(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathFullyQualified(rootPath))
        {
            throw new ArgumentException("Image storage root must be an absolute path.", nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(_rootPath) ||
            string.Equals(Path.GetPathRoot(_rootPath)?.TrimEnd(Path.DirectorySeparatorChar), _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image storage root must not be a filesystem root.", nameof(rootPath));
        }
    }

    public async Task<StoredDeviceImage> SaveAsync(
        Guid imageId,
        ValidatedDeviceImage image,
        CancellationToken cancellationToken = default)
    {
        if (imageId == Guid.Empty)
        {
            throw new ArgumentException("Image identifier cannot be empty.", nameof(imageId));
        }

        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        var extension = image.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException("Unsupported device image content type.", nameof(image)),
        };
        var storageKey = $"images/{imageId:N}{extension}";
        var targetPath = ResolveStoragePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        var originalPosition = image.Content.CanSeek ? image.Content.Position : 0;
        try
        {
            if (image.Content.CanSeek)
            {
                image.Content.Position = 0;
            }

            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await image.Content.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: false);
            return new StoredDeviceImage(
                imageId,
                storageKey,
                image.ContentType,
                image.ByteLength,
                image.PixelWidth,
                image.PixelHeight,
                image.Sha256Hex);
        }
        finally
        {
            if (image.Content.CanSeek)
            {
                image.Content.Position = originalPosition;
            }

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public ValueTask<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveStoragePath(storageKey);
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult(stream);
    }

    private string ResolveStoragePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            Path.IsPathRooted(storageKey) ||
            storageKey.Contains('\0'))
        {
            throw new ArgumentException("Image storage key must be a relative path.", nameof(storageKey));
        }

        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey));
        var rootWithSeparator = _rootPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image storage key escapes the private root.", nameof(storageKey));
        }

        return path;
    }
}
