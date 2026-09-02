using DeviceRental.Web.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages.Admin;

public sealed class PolicyModel(
    IDeviceDeskService deviceDesk,
    DemoCurrentUserContext currentUserContext) : PageModel
{
    public int DefaultLoanMinutes { get; private set; }

    public string? Feedback => TempData["PolicyFeedback"] as string;

    public bool FeedbackSucceeded => TempData["PolicyFeedbackSucceeded"] as bool? ?? false;

    public IActionResult OnGet()
    {
        if (!currentUserContext.GetCurrentUser().IsAdministrator)
        {
            return RedirectToPage("/Index");
        }

        DefaultLoanMinutes = deviceDesk.DefaultLoanMinutes;
        return Page();
    }

    public IActionResult OnPostSave(int minutes, string? reason)
    {
        var user = currentUserContext.GetCurrentUser();
        if (!user.IsAdministrator)
        {
            return RedirectToPage("/Index");
        }

        var result = deviceDesk.SetDefaultLoanMinutes(minutes, reason, user);
        TempData["PolicyFeedback"] = result.Message;
        TempData["PolicyFeedbackSucceeded"] = result.Succeeded;
        return RedirectToPage();
    }
}
