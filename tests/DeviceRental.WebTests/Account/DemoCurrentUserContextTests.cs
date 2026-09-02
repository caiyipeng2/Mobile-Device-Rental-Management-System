using System.Security.Claims;
using DeviceRental.Web.Demo;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Account;

public sealed class DemoCurrentUserContextTests
{
    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-AUTH-007")]
    public void Authenticated_claims_override_demo_fallback_role_and_name()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:CurrentUserName"] = "演示用户",
                ["Demo:CurrentUserRole"] = "User",
            })
            .Build();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "真实管理员"),
            new Claim(ClaimTypes.Role, "TEST_ADMIN"),
        ],
        "DeviceRentalCookie"));

        var user = new DemoCurrentUserContext(configuration).GetCurrentUser(principal);

        Assert.Equal("真实管理员", user.DisplayName);
        Assert.True(user.IsAdministrator);
    }
}
