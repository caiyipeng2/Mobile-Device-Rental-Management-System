using System.ComponentModel.DataAnnotations;
using DeviceRental.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages.Account;

public sealed class ForgotPasswordModel(IAccountApplicationService accountService) : PageModel
{
    [BindProperty]
    public ForgotPasswordForm Input { get; set; } = new();

    public string? ResultMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await accountService.RequestPasswordResetAsync(
            Input.Email,
            DateTimeOffset.UtcNow,
            cancellationToken);
        ResultMessage = "如果该邮箱对应有效账户，密码重置邮件将发送到该邮箱。请在 30 分钟内完成操作。";
        ModelState.Clear();
        Input = new ForgotPasswordForm();
        return Page();
    }

    public sealed class ForgotPasswordForm
    {
        [Required(ErrorMessage = "请输入公司邮箱")]
        [EmailAddress(ErrorMessage = "请输入有效邮箱")]
        [Display(Name = "公司邮箱")]
        public string? Email { get; set; }
    }
}
