using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Web.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<DeviceRentalDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Database");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Database must contain a PostgreSQL 18 connection string.");
    }

    options.UseNpgsql(connectionString, postgres =>
    {
        postgres.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.FullName);
        postgres.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            DeviceRentalDbContext.SchemaName);
    });
});
builder.Services.AddHealthChecks()
    .AddCheck(
        "live",
        () => HealthCheckResult.Healthy("The web process is running."),
        tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"]);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
