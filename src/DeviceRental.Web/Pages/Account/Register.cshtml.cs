using System.ComponentModel.DataAnnotations;
using DeviceRental.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages.Account;

public sealed class RegisterModel(IAccountApplicationService accountService) : PageModel
{
    [BindProperty]
    public RegistrationForm Input { get; set; } = new();

    public string? ResultMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await accountService.RegisterAsync(
            new RegistrationInput(Input.Email, Input.RealName, Input.Password),
            cancellationToken);
        if (result.Outcome == RegistrationOutcome.CreatedPendingEmailVerification)
        {
            ResultMessage = "注册成功。请查收验证邮件，验证后再登录。";
            ModelState.Clear();
            Input = new RegistrationForm();
            return Page();
        }

        if (result.Outcome == RegistrationOutcome.DuplicateEmail)
        {
            ModelState.AddModelError(string.Empty, "注册信息无法处理，请检查邮箱或稍后重试。");
            return Page();
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Field, error.Code);
        }

        return Page();
    }

    public sealed class RegistrationForm
    {
        [Required(ErrorMessage = "请输入公司邮箱")]
        [EmailAddress(ErrorMessage = "请输入有效邮箱")]
        [Display(Name = "公司邮箱")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "请输入真实姓名")]
        [StringLength(200, ErrorMessage = "真实姓名不能超过 200 个字符")]
        [Display(Name = "真实姓名")]
        public string? RealName { get; set; }

        [Required(ErrorMessage = "请输入密码")]
        [StringLength(128, MinimumLength = 12, ErrorMessage = "密码长度需为 12-128 个字符")]
        [DataType(DataType.Password)]
        [Display(Name = "密码")]
        public string? Password { get; set; }
    }
}
