using System.ComponentModel.DataAnnotations;
using DeviceRental.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages.Account;

public sealed class ResetPasswordModel(IAccountApplicationService accountService) : PageModel
{
    [BindProperty]
    public ResetPasswordForm Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public string? ResultMessage { get; private set; }

    public bool IsSuccess { get; private set; }

    public void OnGet()
    {
        Input.Email = Email;
        Input.Token = Token;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!string.Equals(Input.NewPassword, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.ConfirmPassword), "两次输入的密码不一致");
            return Page();
        }

        var result = await accountService.ResetPasswordAsync(
            Input.Email,
            Input.Token,
            Input.NewPassword,
            DateTimeOffset.UtcNow,
            cancellationToken);
        IsSuccess = result.Outcome == PasswordResetOutcome.Reset;
        ResultMessage = result.Outcome switch
        {
            PasswordResetOutcome.Reset => "密码已更新，请使用新密码登录。",
            PasswordResetOutcome.ValidationFailed => "新密码不符合安全要求，请修改后重试。",
            _ => "重置链接无效或已过期，请重新申请密码重置。",
        };
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Field, error.Code);
        }

        return Page();
    }

    public sealed class ResetPasswordForm
    {
        [Required(ErrorMessage = "请输入公司邮箱")]
        [EmailAddress(ErrorMessage = "请输入有效邮箱")]
        [Display(Name = "公司邮箱")]
        public string? Email { get; set; }

        [Required]
        public string? Token { get; set; }

        [Required(ErrorMessage = "请输入新密码")]
        [StringLength(128, MinimumLength = 12, ErrorMessage = "密码长度需为 12-128 个字符")]
        [DataType(DataType.Password)]
        [Display(Name = "新密码")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "请再次输入新密码")]
        [DataType(DataType.Password)]
        [Display(Name = "确认新密码")]
        public string? ConfirmPassword { get; set; }
    }
}
