using DeviceRental.Application.Devices;
using DeviceRental.Domain.Devices;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Persistence.Mappers;
using DeviceRental.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeviceRental.Infrastructure.Devices;

/// <summary>
/// Commits image metadata and its device reference together. The filesystem object is written by
/// the caller before this transaction and can be collected as an unreferenced object on failure.
/// </summary>
public sealed class EfDeviceRegistrationStore(DeviceRentalDbContext dbContext) : IDeviceRegistrationStore
{
    public async Task<DeviceRegistrationStoreResult> RegisterAsync(
        Device device,
        DeviceImageMetadata image,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(image);
        if (device.ImageId != image.Id)
        {
            throw new ArgumentException("The device image reference must match image metadata.", nameof(image));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = image.CreatedAtUtc;
            dbContext.DeviceImages.Add(new DeviceImageMetadataRecord
            {
                Id = image.Id,
                StorageKey = image.StorageKey,
                ContentType = image.ContentType,
                ByteLength = image.ByteLength,
                PixelWidth = image.PixelWidth,
                PixelHeight = image.PixelHeight,
                Sha256Hex = image.Sha256Hex,
                CreatedAtUtc = image.CreatedAtUtc,
            });
            dbContext.Devices.Add(DeviceRecordMapper.ToRecord(device, now, now));

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsAssetConflict(exception))
            {
                dbContext.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken);
                return new DeviceRegistrationStoreResult(DeviceRegistrationStoreStatus.DuplicateAssetNumber);
            }

            await transaction.CommitAsync(cancellationToken);
            return new DeviceRegistrationStoreResult(DeviceRegistrationStoreStatus.Succeeded);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static bool IsAssetConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_devices_asset_number",
        };
}
