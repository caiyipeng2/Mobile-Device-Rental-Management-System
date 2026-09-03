using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Account;

public sealed class AccountPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountPageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=1;Database=unreachable;Username=unreachable;Password=unreachable",
                    ["Identity:AllowedEmailDomains:0"] = "example.com",
                }));
        });
    }

    [Theory]
    [InlineData("/Account/Register", "注册设备台账账户")]
    [InlineData("/Account/Login", "登录设备台账")]
    [InlineData("/Account/VerifyEmail", "验证公司邮箱")]
    [InlineData("/Account/ForgotPassword", "找回密码")]
    [InlineData("/Account/ResetPassword", "重置密码")]
    [Trait("Category", "Web")]
    [Trait("Requirement", "REQ-AUTH-001")]
    public async Task Account_pages_render_without_opening_database_connection(string path, string heading)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(heading, html, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);
        Assert.Contains("auth-aside", html, StringComparison.Ordinal);
        Assert.Contains("auth-card", html, StringComparison.Ordinal);
    }
}
