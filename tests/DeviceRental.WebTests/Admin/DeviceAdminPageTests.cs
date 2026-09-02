using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Admin;

public sealed class DeviceAdminPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DeviceAdminPageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "true",
                    ["Demo:CurrentUserName"] = "陈述",
                    ["Demo:CurrentUserRole"] = "TestAdmin",
                }));
        });
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-DEV-003")]
    public async Task Administrator_can_register_a_new_device()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var page = await client.GetAsync("/Admin/Devices", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = ExtractToken(html);

        using var response = await client.PostAsync(
            "/Admin/Devices?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["assetNumber"] = "QA-NEW-001",
                ["modelName"] = "OnePlus 13",
                ["tier"] = "中端",
                ["imageReference"] = "oneplus-13.webp",
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var after = await client.GetAsync("/Admin/Devices", TestContext.Current.CancellationToken);
        var afterHtml = WebUtility.HtmlDecode(
            await after.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("OnePlus 13", afterHtml, StringComparison.Ordinal);
        Assert.Contains("QA-NEW-001", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-DEV-003")]
    public async Task Standard_user_is_redirected_away_from_device_admin()
    {
        using var userFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Demo:CurrentUserName"] = "林乔",
                    ["Demo:CurrentUserRole"] = "User",
                })));
        using var client = userFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/Admin/Devices", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    private static string ExtractToken(string html)
    {
        var token = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(token.Success, "The admin page must render an anti-forgery token.");
        return WebUtility.HtmlDecode(token.Groups["token"].Value);
    }
}
