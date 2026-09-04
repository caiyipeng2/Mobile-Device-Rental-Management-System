using DeviceRental.Application.Notifications;
using DeviceRental.Infrastructure.Notifications;
using DeviceRental.Infrastructure.Options;
using DeviceRental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DeviceRental.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<DeviceRentalDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>()
        .GetConnectionString("Database");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:Database is required for the notification Worker.");
    }

    options.UseNpgsql(connectionString, postgres =>
    {
        postgres.MigrationsAssembly(typeof(DeviceRentalDbContext).Assembly.FullName);
        postgres.MigrationsHistoryTable("__EFMigrationsHistory", DeviceRentalDbContext.SchemaName);
    });
});
builder.Services.AddScoped<IOutboxStore, PostgresOutboxStore>();
builder.Services.AddScoped<OutboxProcessor>();
builder.Services.AddScoped<INotificationPayloadCodec, AesGcmNotificationPayloadCodec>();
builder.Services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
builder.Services.AddScoped<INotificationSender, SmtpNotificationSender>();
builder.Services.AddScoped<IEmailTransport, SystemNetMailTransport>();
builder.Services.AddOptions<NotificationEncryptionOptions>()
    .Bind(builder.Configuration.GetSection("NotificationEncryption"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<NotificationEncryptionOptions>, NotificationEncryptionOptionsValidator>();
builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection("Smtp"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection("Worker"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<WorkerOptions>, WorkerOptionsValidator>();
builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();
host.Run();
