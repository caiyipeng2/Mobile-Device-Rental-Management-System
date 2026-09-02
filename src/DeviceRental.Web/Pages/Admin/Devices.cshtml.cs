using DeviceRental.Web.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages.Admin;

public sealed class DevicesModel(
    IDeviceDeskService deviceDesk,
    DemoCurrentUserContext currentUserContext) : PageModel
{
    public IReadOnlyList<DeviceDeskDevice> Devices { get; private set; } = [];

    public string? Feedback => TempData["AdminDeviceFeedback"] as string;

    public bool FeedbackSucceeded => TempData["AdminDeviceFeedbackSucceeded"] as bool? ?? false;

    public IActionResult OnGet()
    {
        var user = currentUserContext.GetCurrentUser();
        if (!user.IsAdministrator)
        {
            return RedirectToPage("/Index");
        }

        Load();
        return Page();
    }

    public IActionResult OnPostCreate(
        string? assetNumber,
        string? modelName,
        string? tier,
        string? imageReference)
    {
        var user = currentUserContext.GetCurrentUser();
        if (!user.IsAdministrator)
        {
            return RedirectToPage("/Index");
        }

        var result = deviceDesk.AddDevice(assetNumber ?? string.Empty, modelName ?? string.Empty, tier ?? string.Empty, imageReference, user);
        TempData["AdminDeviceFeedback"] = result.Message;
        TempData["AdminDeviceFeedbackSucceeded"] = result.Succeeded;
        return RedirectToPage();
    }

    private void Load() => Devices = deviceDesk.GetOverview(null).Devices;
}
