using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Admin;

public sealed class LoanPolicyPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LoanPolicyPageTests(WebApplicationFactory<Program> factory)
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
    [Trait("Requirement", "REQ-ADMIN-001")]
    [Trait("Requirement", "REQ-ADMIN-002")]
    public async Task Administrator_can_change_default_loan_duration_for_future_loans()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var page = await client.GetAsync("/Admin/Policy", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = ExtractToken(html);

        using var response = await client.PostAsync(
            "/Admin/Policy?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["minutes"] = "120",
                ["reason"] = "夜间回归测试窗口",
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var after = await client.GetAsync("/Admin/Policy", TestContext.Current.CancellationToken);
        var afterHtml = WebUtility.HtmlDecode(
            await after.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("120 分钟", afterHtml, StringComparison.Ordinal);
        Assert.Contains("夜间回归测试窗口", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-ADMIN-001")]
    public async Task Policy_change_requires_a_reason()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        using var page = await client.GetAsync("/Admin/Policy", TestContext.Current.CancellationToken);
        var token = ExtractToken(await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        using var response = await client.PostAsync(
            "/Admin/Policy?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["minutes"] = "120",
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var after = await client.GetAsync("/Admin/Policy", TestContext.Current.CancellationToken);
        var html = WebUtility.HtmlDecode(
            await after.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("修改借期时必须填写原因", html, StringComparison.Ordinal);
    }

    private static string ExtractToken(string html)
    {
        var token = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(token.Success, "The policy page must render an anti-forgery token.");
        return WebUtility.HtmlDecode(token.Groups["token"].Value);
    }
}
