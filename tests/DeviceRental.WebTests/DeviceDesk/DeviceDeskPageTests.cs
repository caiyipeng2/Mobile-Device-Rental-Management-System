using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.DeviceDesk;

public sealed class DeviceDeskPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DeviceDeskPageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "true",
                    ["Demo:CurrentUserName"] = "林乔",
                    ["Demo:CurrentUserRole"] = "User",
                }));
        });
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task Index_ShowsDeviceInventoryAndBorrowActions()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("测试设备台账", html, StringComparison.Ordinal);
        Assert.Contains("iPhone 16 Pro", html, StringComparison.Ordinal);
        Assert.Contains("借用", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task Index_StatusFilterShowsOnlyRequestedAvailability()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/?status=Available", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("iPhone 16 Pro", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Galaxy S24 Ultra", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task BorrowThenReturn_ChangesTheCurrentUsersDeviceAction()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var borrow = await PostActionAsync(client, "Borrow", "NAT-021");
        Assert.Equal(HttpStatusCode.Redirect, borrow.StatusCode);

        using var afterBorrow = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var borrowedHtml = await afterBorrow.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("林乔（本人）", WebUtility.HtmlDecode(borrowedHtml), StringComparison.Ordinal);
        Assert.Contains("归还", borrowedHtml, StringComparison.Ordinal);

        using var returnResponse = await PostActionAsync(client, "Return", "NAT-021");
        Assert.Equal(HttpStatusCode.Redirect, returnResponse.StatusCode);

        using var afterReturn = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var returnedHtml = await afterReturn.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("借用", returnedHtml, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    public async Task Administrator_CanSetTemporarilyUnavailableWithReason()
    {
        using var adminFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "true",
                    ["Demo:CurrentUserName"] = "陈述",
                    ["Demo:CurrentUserRole"] = "TestAdmin",
                })));
        using var client = adminFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var update = await PostActionAsync(
            client,
            "SetAvailability",
            "LAB-019",
            new Dictionary<string, string>
            {
                ["availability"] = "Unavailable",
                ["reason"] = "摄像头检修",
            });

        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);

        using var afterUpdate = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await afterUpdate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("暂不可借", html, StringComparison.Ordinal);
        Assert.Contains("摄像头检修", html, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PostActionAsync(
        HttpClient client,
        string handler,
        string deviceId,
        IReadOnlyDictionary<string, string>? values = null)
    {
        using var page = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.IgnoreCase);

        Assert.True(token.Success, "The device desk must render an anti-forgery protected action form.");

        var form = new Dictionary<string, string>(values ?? new Dictionary<string, string>())
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups["token"].Value),
        };

        return await client.PostAsync(
            $"/?handler={handler}&deviceId={Uri.EscapeDataString(deviceId)}",
            new FormUrlEncodedContent(form),
            TestContext.Current.CancellationToken);
    }
}
