using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DeviceRental.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages.Account;

public sealed class LoginModel(
    IAccountApplicationService accountService) : PageModel
{
    [BindProperty]
    public LoginForm Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await accountService.SignInAsync(
            Input.Email,
            Input.Password,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (result.Outcome == SignInOutcome.Authenticated && result.Account is not null)
        {
            var role = result.Account.Role == AccountRole.TestAdmin ? "TEST_ADMIN" : "USER";
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, result.Account.Id.ToString()),
                new Claim(ClaimTypes.Name, result.Account.RealName),
                new Claim(ClaimTypes.Email, result.Account.Email),
                new Claim(ClaimTypes.Role, role),
            };
            var identity = new ClaimsIdentity(claims, "DeviceRentalCookie");
            await HttpContext.SignInAsync(
                "DeviceRentalCookie",
                new ClaimsPrincipal(identity));
            return RedirectToPage("/Index");
        }

        ModelState.AddModelError(
            string.Empty,
            result.Outcome == SignInOutcome.Locked
                ? "登录尝试过多，请稍后再试。"
                : "邮箱或密码不正确，或账户尚未验证。请检查后重试。");
        return Page();
    }

    public sealed class LoginForm
    {
        [Required(ErrorMessage = "请输入公司邮箱")]
        [EmailAddress(ErrorMessage = "请输入有效邮箱")]
        [Display(Name = "公司邮箱")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "请输入密码")]
        [DataType(DataType.Password)]
        [Display(Name = "密码")]
        public string? Password { get; set; }
    }
}
