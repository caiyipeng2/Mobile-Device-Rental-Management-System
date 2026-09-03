using System.ComponentModel.DataAnnotations;
using DeviceRental.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages.Account;

public sealed class VerifyEmailModel(IAccountApplicationService accountService) : PageModel
{
    [BindProperty]
    public VerificationForm Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    public string? ResultMessage { get; private set; }

    public bool IsSuccess { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Token))
        {
            return;
        }

        var result = await accountService.VerifyEmailAsync(
            Email,
            Token,
            DateTimeOffset.UtcNow,
            cancellationToken);
        IsSuccess = result.Outcome is EmailVerificationOutcome.Verified or EmailVerificationOutcome.AlreadyVerified;
        ResultMessage = result.Outcome switch
        {
            EmailVerificationOutcome.Verified => "邮箱验证成功，现在可以登录设备台账。",
            EmailVerificationOutcome.AlreadyVerified => "该邮箱已经验证，可以直接登录设备台账。",
            _ => "验证链接无效或已过期，请重新发送验证邮件。",
        };
    }

    public async Task<IActionResult> OnPostResendAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await accountService.RequestEmailVerificationAsync(
            Input.Email,
            DateTimeOffset.UtcNow,
            cancellationToken);
        ResultMessage = "如果该邮箱对应未验证账户，验证邮件将发送到该邮箱。请查收邮件并在 24 小时内完成验证。";
        ModelState.Clear();
        Input = new VerificationForm();
        return Page();
    }

    public sealed class VerificationForm
    {
        [Required(ErrorMessage = "请输入公司邮箱")]
        [EmailAddress(ErrorMessage = "请输入有效邮箱")]
        [Display(Name = "公司邮箱")]
        public string? Email { get; set; }
    }
}
