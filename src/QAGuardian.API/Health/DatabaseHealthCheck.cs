using Microsoft.Extensions.Diagnostics.HealthChecks;
using QAGuardian.Infrastructure.Persistence;

namespace QAGuardian.API.Health;

/// <summary>Verifica conectividad a la base (SQL Server o SQLite) para readiness.</summary>
public sealed class DatabaseHealthCheck(QAGuardianDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await db.Database.CanConnectAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy("Base de datos accesible.")
                : HealthCheckResult.Unhealthy("No se pudo conectar a la base de datos.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error al verificar la base de datos.", ex);
        }
    }
}
