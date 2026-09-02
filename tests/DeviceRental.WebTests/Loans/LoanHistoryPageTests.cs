using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Loans;

public sealed class LoanHistoryPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LoanHistoryPageTests(WebApplicationFactory<Program> factory)
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
    [Trait("Requirement", "REQ-LOAN-014")]
    public async Task History_page_shows_current_user_scope_and_seeded_records()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Loans", TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("我的借用", html, StringComparison.Ordinal);
        Assert.Contains("林乔", html, StringComparison.Ordinal);
        Assert.DoesNotContain("王蕾", html, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-LOAN-014")]
    public async Task Borrowed_device_appears_on_history_page()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var page = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = ExtractToken(html);
        using var borrow = await client.PostAsync(
            "/?handler=Borrow&deviceId=LAB-019",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, borrow.StatusCode);

        using var history = await client.GetAsync("/Loans", TestContext.Current.CancellationToken);
        var historyHtml = WebUtility.HtmlDecode(
            await history.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Contains("iPhone 13 mini", historyHtml, StringComparison.Ordinal);
        Assert.Contains("进行中", historyHtml, StringComparison.Ordinal);
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
