using DeviceRental.Application.Devices;
using DeviceRental.Domain.Devices;
using DeviceRental.Web.Demo;

namespace DeviceRental.Web.Database;

public interface IDeviceIntakeService
{
    Task<DeviceDeskOperationResult> RegisterAsync(
        string assetNumber,
        string modelName,
        string tier,
        Stream imageContent,
        DemoCurrentUser user,
        CancellationToken cancellationToken = default);
}

public sealed class DatabaseDeviceIntakeService(
    DeviceImageUploadPolicy uploadPolicy,
    IDeviceRegistrationStore registrationStore,
    IEnumerable<IDeviceImageStorage> imageStorages) : IDeviceIntakeService
{
    public async Task<DeviceDeskOperationResult> RegisterAsync(
        string assetNumber,
        string modelName,
        string tier,
        Stream imageContent,
        DemoCurrentUser user,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsAdministrator || user.UserId is null)
        {
            return DeviceDeskOperationResult.Failure("只有测试组管理员可以新增设备。");
        }

        if (string.IsNullOrWhiteSpace(assetNumber) || string.IsNullOrWhiteSpace(modelName))
        {
            return DeviceDeskOperationResult.Failure("资产编号和型号名称均为必填项。");
        }

        if (!TryParseTier(tier, out var deviceTier))
        {
            return DeviceDeskOperationResult.Failure("档位必须选择低端、中端或高端。");
        }

        ArgumentNullException.ThrowIfNull(imageContent);
        var storage = imageStorages.SingleOrDefault();
        if (storage is null)
        {
            return DeviceDeskOperationResult.Failure("图片存储尚未配置，请联系系统管理员。");
        }

        ValidatedDeviceImage validated;
        try
        {
            validated = await uploadPolicy.ValidateAsync(imageContent, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return DeviceDeskOperationResult.Failure(exception.Message);
        }

        await using (validated.Content)
        {
            var imageId = Guid.NewGuid();
            var stored = await storage.SaveAsync(imageId, validated, cancellationToken);
            var metadata = new DeviceImageMetadata(
                stored.Id,
                stored.StorageKey,
                stored.ContentType,
                stored.ByteLength,
                stored.PixelWidth,
                stored.PixelHeight,
                stored.Sha256Hex,
                DateTimeOffset.UtcNow);
            var device = new Device(
                Guid.NewGuid(),
                assetNumber,
                modelName,
                deviceTier,
                metadata.Id);
            var result = await registrationStore.RegisterAsync(device, metadata, cancellationToken);
            return result.Status switch
            {
                DeviceRegistrationStoreStatus.Succeeded =>
                    DeviceDeskOperationResult.Success($"已新增设备 {device.ModelName}（{device.AssetNumber}）。"),
                DeviceRegistrationStoreStatus.DuplicateAssetNumber =>
                    DeviceDeskOperationResult.Failure("资产编号已存在，请换一个编号。"),
                _ => DeviceDeskOperationResult.Failure("设备新增未完成，请稍后重试。"),
            };
        }
    }

    private static bool TryParseTier(string? value, out DeviceTier tier)
    {
        tier = value?.Trim() switch
        {
            "低端" => DeviceTier.Low,
            "中端" => DeviceTier.Mid,
            "高端" => DeviceTier.High,
            _ => default,
        };
        return value?.Trim() is "低端" or "中端" or "高端";
    }
}
