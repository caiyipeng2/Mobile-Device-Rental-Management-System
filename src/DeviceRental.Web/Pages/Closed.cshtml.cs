using DeviceRental.Application.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeviceRental.Web.Pages;

public sealed class ClosedModel(AccessWindowPolicy accessWindowPolicy) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DateTimeOffset? NextOpenUtc { get; set; }

    public string NextOpenLocal =>
        (NextOpenUtc ?? accessWindowPolicy.Evaluate(DateTimeOffset.UtcNow).NextOpenUtc ?? DateTimeOffset.UtcNow)
        .ToOffset(TimeSpan.FromHours(8))
        .ToString("yyyy-MM-dd HH:mm");
}
