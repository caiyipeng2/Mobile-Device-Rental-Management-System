using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeviceRental.WebTests.Health;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    [Trait("Category", "Web")]
    [Trait("Requirement", "NFR-REL-001")]
    public async Task Live_WithoutDatabaseConfiguration_ReturnsOk()
    {
        using var client = CreateClient(_factory);

        using var response = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-connection-string")]
    [Trait("Category", "Web")]
    [Trait("Requirement", "NFR-REL-001")]
    public async Task Ready_WithoutUsableDatabaseConfiguration_ReturnsServiceUnavailable(
        string connectionString)
    {
        using var configuredFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = connectionString,
                })));
        using var client = CreateClient(configuredFactory);

        using var response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
}
