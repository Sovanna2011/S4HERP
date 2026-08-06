using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace S4HERP.Api.Data;

public class DatabaseHealthCheck(AppDbContext db, MigrationState migrations) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("SQL Server is not reachable.");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server connection failed.", ex);
        }

        return migrations.Completed
            ? HealthCheckResult.Healthy("SQL Server is reachable and migrations are applied.")
            : HealthCheckResult.Degraded("SQL Server is reachable; migrations are still running.");
    }
}
