using System.Net;
using DeviceRental.Application.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Account;

public sealed class ProductionAccessGuardTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProductionAccessGuardTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "false",
                    ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=1;Database=unreachable;Username=unreachable;Password=unreachable",
                }));
        });
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task Rental_pages_redirect_anonymous_non_demo_users_to_login()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString;
        var accessWindow = new AccessWindowPolicy().Evaluate(DateTimeOffset.UtcNow);
        if (accessWindow.IsOpen)
        {
            Assert.Equal("/Account/Login?returnUrl=%2F", location);
        }
        else
        {
            Assert.StartsWith("/Closed?nextOpenUtc=", location, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task Account_pages_remain_public_for_non_demo_users()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
