using DeviceRental.Web.Demo;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages;

public sealed class LoansModel(
    IDeviceDeskService deviceDesk,
    DemoCurrentUserContext currentUserContext) : PageModel
{
    public IReadOnlyList<DeviceDeskLoan> Loans { get; private set; } = [];

    public bool IsAdministrator { get; private set; }

    public void OnGet()
    {
        var user = currentUserContext.GetCurrentUser(User);
        IsAdministrator = user.IsAdministrator;
        Loans = deviceDesk.GetLoans(user);
    }
}
