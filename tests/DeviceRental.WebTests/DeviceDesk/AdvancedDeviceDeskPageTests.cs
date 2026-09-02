using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.DeviceDesk;

public sealed class AdvancedDeviceDeskPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdvancedDeviceDeskPageTests(WebApplicationFactory<Program> factory)
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
    [Trait("Requirement", "REQ-DEV-002")]
    public async Task Search_filters_by_model_or_asset_number()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/?search=Pixel", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Pixel 9", html, StringComparison.Ordinal);
        Assert.DoesNotContain("iPhone 16 Pro", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-LOAN-008")]
    public async Task Administrator_force_return_requires_reason_and_releases_device()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var page = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = ExtractToken(html);

        using var missingReason = await client.PostAsync(
            "/?handler=ForceReturn&deviceId=QA-014",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, missingReason.StatusCode);

        using var afterDenied = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var deniedHtml = WebUtility.HtmlDecode(
            await afterDenied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("强制归还需要填写原因", deniedHtml, StringComparison.Ordinal);

        token = ExtractToken(deniedHtml);
        using var forced = await client.PostAsync(
            "/?handler=ForceReturn&deviceId=QA-014",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["reason"] = "设备送修",
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, forced.StatusCode);

        using var afterReturn = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var returnedHtml = WebUtility.HtmlDecode(
            await afterReturn.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("设备送修", returnedHtml, StringComparison.Ordinal);
        Assert.Contains("可立即借用", returnedHtml, StringComparison.Ordinal);
    }

    private static string ExtractToken(string html)
    {
        var token = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(token.Success, "The device desk must render an anti-forgery token.");
        return WebUtility.HtmlDecode(token.Groups["token"].Value);
    }
}
