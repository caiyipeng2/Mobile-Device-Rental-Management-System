using DeviceRental.Web.Demo;
using DeviceRental.Web.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceRental.Web.Pages.Admin;

public sealed class DevicesModel(
    IDeviceDeskService deviceDesk,
    DemoCurrentUserContext currentUserContext,
    IServiceProvider services) : PageModel
{
    [BindProperty]
    public IFormFile? Image { get; set; }
    public IReadOnlyList<DeviceDeskDevice> Devices { get; private set; } = [];

    public string? Feedback => TempData["AdminDeviceFeedback"] as string;

    public bool FeedbackSucceeded => TempData["AdminDeviceFeedbackSucceeded"] as bool? ?? false;

    public IActionResult OnGet()
    {
        var user = currentUserContext.GetCurrentUser(User);
        if (!user.IsAdministrator)
        {
            return RedirectToPage("/Index");
        }

        Load();
        return Page();
    }

    public async Task<IActionResult> OnPostCreate(
        string? assetNumber,
        string? modelName,
        string? tier,
        string? imageReference)
    {
        var user = currentUserContext.GetCurrentUser(User);
        if (!user.IsAdministrator)
        {
            return RedirectToPage("/Index");
        }

        DeviceDeskOperationResult result;
        if (currentUserContext.IsDemoEnabled)
        {
            result = deviceDesk.AddDevice(assetNumber ?? string.Empty, modelName ?? string.Empty, tier ?? string.Empty, imageReference, user);
        }
        else if (Image is null || Image.Length == 0)
        {
            result = DeviceDeskOperationResult.Failure("新增设备必须上传展示图。");
        }
        else
        {
            await using var imageStream = Image.OpenReadStream();
            result = await services.GetRequiredService<IDeviceIntakeService>().RegisterAsync(
                assetNumber ?? string.Empty,
                modelName ?? string.Empty,
                tier ?? string.Empty,
                imageStream,
                user,
                HttpContext.RequestAborted);
        }
        TempData["AdminDeviceFeedback"] = result.Message;
        TempData["AdminDeviceFeedbackSucceeded"] = result.Succeeded;
        return RedirectToPage();
    }

    private void Load() => Devices = deviceDesk.GetOverview(null).Devices;
}
