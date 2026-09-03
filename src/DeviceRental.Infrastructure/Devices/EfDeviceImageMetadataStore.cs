using DeviceRental.Application.Devices;
using DeviceRental.Domain.Devices;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace DeviceRental.Infrastructure.Devices;

public sealed class EfDeviceImageMetadataStore(DeviceRentalDbContext dbContext) : IDeviceImageMetadataStore
{
    public async Task SaveAsync(
        DeviceImageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.Set<DeviceImageMetadataRecord>().Add(new DeviceImageMetadataRecord
        {
            Id = metadata.Id,
            StorageKey = metadata.StorageKey,
            ContentType = metadata.ContentType,
            ByteLength = metadata.ByteLength,
            PixelWidth = metadata.PixelWidth,
            PixelHeight = metadata.PixelHeight,
            Sha256Hex = metadata.Sha256Hex,
            CreatedAtUtc = metadata.CreatedAtUtc,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeviceImageMetadata?> FindAsync(
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var record = await dbContext.Set<DeviceImageMetadataRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(image => image.Id == imageId, cancellationToken);
        return record is null
            ? null
            : new DeviceImageMetadata(
                record.Id,
                record.StorageKey,
                record.ContentType,
                record.ByteLength,
                record.PixelWidth,
                record.PixelHeight,
                record.Sha256Hex,
                record.CreatedAtUtc);
    }
}
