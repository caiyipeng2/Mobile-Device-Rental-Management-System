using DeviceRental.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeviceRental.Web.Health;

public sealed class DatabaseReadinessHealthCheck(IServiceScopeFactory scopeFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<DeviceRentalDbContext>();
            await MigrationReadinessVerifier.VerifyAsync(database, cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL 18 is reachable and the schema is current.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL 18 readiness verification failed.",
                exception);
        }
    }
}
