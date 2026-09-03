using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Ui;

public sealed class StaticAssetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StaticAssetTests(WebApplicationFactory<Program> factory)
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
    public async Task Device_stylesheet_is_non_empty_when_browser_requests_compression()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/css/device-desk.css");
        request.Headers.AcceptEncoding.ParseAdd("gzip, br");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var css = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("--desk-canvas", css, StringComparison.Ordinal);
    }
}
