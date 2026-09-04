using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DeviceRental.Infrastructure.Options;

public sealed class SmtpOptions
{
    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65_535)]
    public int Port { get; set; } = 587;

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool UseTls { get; set; } = true;
}

public sealed class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host)) failures.Add("SMTP 主机不能为空。");
        if (options.Port is < 1 or > 65_535) failures.Add("SMTP 端口必须在 1-65535 之间。");
        if (string.IsNullOrWhiteSpace(options.FromAddress) || !new EmailAddressAttribute().IsValid(options.FromAddress)) failures.Add("发件邮箱必须是有效邮箱。");
        if (string.IsNullOrWhiteSpace(options.Username)) failures.Add("SMTP 用户名不能为空。");
        if (string.IsNullOrWhiteSpace(options.Password)) failures.Add("SMTP 密码不能为空。");
        if (!options.UseTls) failures.Add("SMTP 必须启用 TLS。");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
