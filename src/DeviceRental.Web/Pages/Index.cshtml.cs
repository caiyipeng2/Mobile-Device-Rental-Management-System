using DeviceRental.Web.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages;

public sealed class IndexModel(
    IDeviceDeskService deviceDesk,
    DemoCurrentUserContext currentUserContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public DeviceDeskOverview Overview { get; private set; } = new([], new DeviceDeskSummary(0, 0, 0, 0));

    public DemoCurrentUser CurrentUser { get; private set; } = new("", false);

    public string? Feedback => TempData["DeviceDeskFeedback"] as string;

    public bool FeedbackSucceeded => TempData["DeviceDeskFeedbackSucceeded"] as bool? ?? false;

    public void OnGet() => Load();

    public IActionResult OnPostBorrow(string deviceId)
    {
        StoreFeedback(deviceDesk.Borrow(deviceId, GetCurrentUser(), DateTimeOffset.UtcNow));
        return RedirectToPage(new { Status });
    }

    public IActionResult OnPostReturn(string deviceId)
    {
        StoreFeedback(deviceDesk.Return(deviceId, GetCurrentUser(), DateTimeOffset.UtcNow));
        return RedirectToPage(new { Status });
    }

    public IActionResult OnPostSetAvailability(string deviceId, string availability, string? reason)
    {
        var parsedAvailability = Enum.TryParse<DeviceDeskAvailability>(availability, true, out var value)
            ? value
            : DeviceDeskAvailability.Unavailable;

        StoreFeedback(deviceDesk.SetAvailability(deviceId, parsedAvailability, reason, GetCurrentUser()));
        return RedirectToPage(new { Status });
    }

    public string FilterUrl(DeviceDeskAvailability? availability) => availability?.ToString() switch
    {
        null => Url.Page("/Index") ?? "/",
        var value => Url.Page("/Index", new { status = value }) ?? "/",
    };

    public bool IsSelected(DeviceDeskAvailability? availability) => ParseAvailability() == availability;

    private void Load()
    {
        CurrentUser = GetCurrentUser();
        Overview = deviceDesk.GetOverview(ParseAvailability());
    }

    private DemoCurrentUser GetCurrentUser() => currentUserContext.GetCurrentUser();

    private DeviceDeskAvailability? ParseAvailability() =>
        Enum.TryParse<DeviceDeskAvailability>(Status, true, out var value) ? value : null;

    private void StoreFeedback(DeviceDeskOperationResult result)
    {
        TempData["DeviceDeskFeedback"] = result.Message;
        TempData["DeviceDeskFeedbackSucceeded"] = result.Succeeded;
    }
}
