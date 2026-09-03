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

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public DeviceDeskOverview Overview { get; private set; } = new([], new DeviceDeskSummary(0, 0, 0, 0));

    public DemoCurrentUser CurrentUser { get; private set; } = new("", false);

    public bool IsDemoMode { get; private set; }

    public string? Feedback => TempData["DeviceDeskFeedback"] as string;

    public bool FeedbackSucceeded => TempData["DeviceDeskFeedbackSucceeded"] as bool? ?? false;

    public void OnGet() => Load();

    public IActionResult OnPostBorrow(string deviceId)
    {
        StoreFeedback(deviceDesk.Borrow(deviceId, GetCurrentUser(), DateTimeOffset.UtcNow));
        return RedirectToPage(new { Status, Search });
    }

    public IActionResult OnPostReturn(string deviceId)
    {
        StoreFeedback(deviceDesk.Return(deviceId, GetCurrentUser(), DateTimeOffset.UtcNow));
        return RedirectToPage(new { Status, Search });
    }

    public IActionResult OnPostForceReturn(string deviceId, string? reason)
    {
        StoreFeedback(deviceDesk.ForceReturn(deviceId, GetCurrentUser(), DateTimeOffset.UtcNow, reason));
        return RedirectToPage(new { Status, Search });
    }

    public IActionResult OnPostSetAvailability(string deviceId, string availability, string? reason)
    {
        var parsedAvailability = Enum.TryParse<DeviceDeskAvailability>(availability, true, out var value)
            ? value
            : DeviceDeskAvailability.Unavailable;

        StoreFeedback(deviceDesk.SetAvailability(deviceId, parsedAvailability, reason, GetCurrentUser()));
        return RedirectToPage(new { Status, Search });
    }

    public string FilterUrl(DeviceDeskAvailability? availability) =>
        Url.Page("/Index", new
        {
            status = availability?.ToString(),
            search = Search,
        }) ?? "/";

    public bool IsSelected(DeviceDeskAvailability? availability) => ParseAvailability() == availability;

    private void Load()
    {
        CurrentUser = GetCurrentUser();
        IsDemoMode = currentUserContext.IsDemoEnabled;
        Overview = deviceDesk.GetOverview(ParseAvailability(), Search);
    }

    private DemoCurrentUser GetCurrentUser() => currentUserContext.GetCurrentUser(User);

    private DeviceDeskAvailability? ParseAvailability() =>
        Enum.TryParse<DeviceDeskAvailability>(Status, true, out var value) ? value : null;

    private void StoreFeedback(DeviceDeskOperationResult result)
    {
        TempData["DeviceDeskFeedback"] = result.Message;
        TempData["DeviceDeskFeedbackSucceeded"] = result.Succeeded;
    }
}
