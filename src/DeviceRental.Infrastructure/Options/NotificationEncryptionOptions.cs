using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DeviceRental.Infrastructure.Options;

public sealed class NotificationEncryptionOptions
{
    [Required]
    public string CurrentKeyVersion { get; set; } = string.Empty;

    [Required]
    public string CurrentKeyBase64 { get; set; } = string.Empty;
}

public sealed class NotificationEncryptionOptionsValidator : IValidateOptions<NotificationEncryptionOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationEncryptionOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.CurrentKeyVersion)) failures.Add("通知加密密钥版本不能为空。");
        if (string.IsNullOrWhiteSpace(options.CurrentKeyBase64)) failures.Add("通知加密密钥不能为空。");
        else
        {
            try
            {
                var key = Convert.FromBase64String(options.CurrentKeyBase64);
                if (key.Length is not (16 or 24 or 32)) failures.Add("通知加密密钥必须为 128、192 或 256 位。");
            }
            catch (FormatException)
            {
                failures.Add("通知加密密钥必须是有效的 Base64。");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
