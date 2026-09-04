using DeviceRental.Application.Identity;
using DeviceRental.Application.Devices;
using DeviceRental.Application.Notifications;
using DeviceRental.Application.Policy;
using DeviceRental.Infrastructure.Identity;
using DeviceRental.Infrastructure.Images;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Options;
using DeviceRental.Infrastructure.Persistence;
using DeviceRental.Infrastructure.Devices;
using DeviceRental.Web.Database;
using DeviceRental.Web.Demo;
using DeviceRental.Web.Health;
using DeviceRental.Web.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<InMemoryDeviceDeskService>();
builder.Services.AddScoped<IDeviceCatalogStore, EfDeviceCatalogStore>();
builder.Services.AddOptions<NotificationEncryptionOptions>()
    .Bind(builder.Configuration.GetSection("NotificationEncryption"));
builder.Services.AddScoped<INotificationOutboxWriter>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    if (configuration.GetValue<bool>("Demo:Enabled"))
    {
        return new NoopNotificationOutboxWriter();
    }

    return string.IsNullOrWhiteSpace(configuration["NotificationEncryption:CurrentKeyBase64"])
        ? new UnconfiguredNotificationOutboxWriter()
        : services.GetRequiredService<EfNotificationOutboxWriter>();
});
builder.Services.AddScoped<EfNotificationOutboxWriter>();
builder.Services.AddScoped<INotificationPayloadCodec, AesGcmNotificationPayloadCodec>();
builder.Services.AddScoped<ILoanPolicyStore, EfLoanPolicyStore>();
builder.Services.AddScoped<DatabaseDeviceDeskService>();
builder.Services.AddScoped<IDeviceDeskService>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    return configuration.GetValue<bool>("Demo:Enabled")
        ? services.GetRequiredService<InMemoryDeviceDeskService>()
        : services.GetRequiredService<DatabaseDeviceDeskService>();
});
builder.Services.AddSingleton<DemoCurrentUserContext>();
builder.Services.AddSingleton<AccessWindowPolicy>();
builder.Services.AddSingleton<IDeviceImageDecoder, SkiaSharpDeviceImageDecoder>();
builder.Services.AddSingleton<DeviceImageUploadPolicy>();
builder.Services.AddScoped<IDeviceImageMetadataStore, EfDeviceImageMetadataStore>();
builder.Services.AddScoped<IDeviceRegistrationStore, EfDeviceRegistrationStore>();
builder.Services.AddScoped<IDeviceIntakeService, DatabaseDeviceIntakeService>();
var imageStorageRoot = builder.Configuration["Storage:DeviceImageRoot"];
if (!string.IsNullOrWhiteSpace(imageStorageRoot))
{
    builder.Services.AddSingleton<IDeviceImageStorage>(_ =>
        new FileSystemDeviceImageStorage(imageStorageRoot));
}
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
        options.Tokens.EmailConfirmationTokenProvider = "EmailConfirmation";
        options.Tokens.PasswordResetTokenProvider = "PasswordReset";
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
    .AddEntityFrameworkStores<DeviceRentalDbContext>()
    .AddTokenProvider<EmailConfirmationTokenProvider>("EmailConfirmation")
    .AddTokenProvider<PasswordResetTokenProvider>("PasswordReset");
builder.Services.Configure<EmailConfirmationTokenProviderOptions>(_ => { });
builder.Services.Configure<PasswordResetTokenProviderOptions>(_ => { });
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
    var currentUserContext = context.RequestServices.GetRequiredService<DemoCurrentUserContext>();
    var isDemoTesting = app.Environment.IsEnvironment("Testing") && currentUserContext.IsDemoEnabled;
    var isPublicPath = context.Request.Path.StartsWithSegments("/health") ||
        context.Request.Path.StartsWithSegments("/Closed") ||
        context.Request.Path.StartsWithSegments("/css") ||
        context.Request.Path.StartsWithSegments("/vendor") ||
        context.Request.Path.StartsWithSegments("/favicon.ico") ||
        context.Request.Path.StartsWithSegments("/Account") ||
        context.Request.Path.StartsWithSegments("/Privacy");

    if (isDemoTesting || isPublicPath)
    {
        await next();
        return;
    }

    var accessWindow = context.RequestServices.GetRequiredService<AccessWindowPolicy>()
        .Evaluate(DateTimeOffset.UtcNow);
    if (!accessWindow.IsOpen)
    {
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
        return;
    }

    if (context.User.Identity?.IsAuthenticated != true)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new { code = "AUTHENTICATION_REQUIRED" });
            return;
        }

        var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    await next();
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
app.MapGet("/devices/{deviceId:guid}/image", async (
    Guid deviceId,
    DeviceRentalDbContext dbContext,
    IDeviceImageMetadataStore metadataStore,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var storage = httpContext.RequestServices.GetService<IDeviceImageStorage>();
    if (storage is null)
    {
        return Results.Problem(
            "Private device image storage is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var device = await dbContext.Devices
        .AsNoTracking()
        .SingleOrDefaultAsync(value => value.Id == deviceId, cancellationToken);
    if (device is null || device.IsArchived)
    {
        return Results.NotFound();
    }

    var metadata = await metadataStore.FindAsync(device.ImageId, cancellationToken);
    if (metadata is null)
    {
        return Results.NotFound();
    }

    Stream image;
    try
    {
        image = await storage.OpenReadAsync(metadata.StorageKey, cancellationToken);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound();
    }

    httpContext.Response.Headers.CacheControl = "private, no-store";
    httpContext.Response.Headers.Vary = "Cookie";
    httpContext.Response.Headers.XContentTypeOptions = "nosniff";
    httpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
    httpContext.Response.ContentLength = metadata.ByteLength;
    return Results.Stream(image, metadata.ContentType, enableRangeProcessing: false);
});
app.UseStaticFiles();
app.MapRazorPages();

app.Run();

public partial class Program;
