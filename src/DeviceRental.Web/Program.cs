using DeviceRental.Application.Identity;
using DeviceRental.Application.Devices;
using DeviceRental.Application.Policy;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Images;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Web.Demo;
using DeviceRental.Web.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<IDeviceDeskService, InMemoryDeviceDeskService>();
builder.Services.AddSingleton<DemoCurrentUserContext>();
builder.Services.AddSingleton<AccessWindowPolicy>();
builder.Services.AddSingleton<IDeviceImageDecoder, SkiaSharpDeviceImageDecoder>();
builder.Services.AddSingleton<DeviceImageUploadPolicy>();
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
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
    .AddEntityFrameworkStores<DeviceRentalDbContext>();
builder.Services.AddScoped<IAccountStore, IdentityAccountStore>();
builder.Services.AddScoped<IAccountApplicationService>(services =>
    new AccountApplicationService(
        new CorporateEmailPolicy(
            services.GetRequiredService<IConfiguration>()
                .GetSection("Identity:AllowedEmailDomains")
                .Get<string[]>() ?? ["example.com"]),
        services.GetRequiredService<IAccountStore>()));
builder.Services.AddAuthentication("DeviceRentalCookie")
    .AddCookie("DeviceRentalCookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
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

app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (app.Environment.IsEnvironment("Testing") ||
        context.Request.Path.StartsWithSegments("/health") ||
        context.Request.Path.StartsWithSegments("/Closed") ||
        context.Request.Path.StartsWithSegments("/css") ||
        context.Request.Path.StartsWithSegments("/vendor") ||
        context.Request.Path.StartsWithSegments("/favicon.ico"))
    {
        await next();
        return;
    }

    var accessWindow = context.RequestServices.GetRequiredService<AccessWindowPolicy>()
        .Evaluate(DateTimeOffset.UtcNow);
    if (accessWindow.IsOpen)
    {
        await next();
        return;
    }

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            code = "OUTSIDE_ACCESS_WINDOW",
            nextOpenUtc = accessWindow.NextOpenUtc,
        });
        return;
    }

    context.Response.Redirect($"/Closed?nextOpenUtc={Uri.EscapeDataString(accessWindow.NextOpenUtc!.Value.ToString("O"))}");
});
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
