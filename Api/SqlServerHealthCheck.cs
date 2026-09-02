using Microsoft.Extensions.Diagnostics.HealthChecks;
using SuperMWeb.Infrastructure.Data;

namespace SuperMWeb.Api;

// Health check custom para SQL Server: abre una conexion y ejecuta SELECT 1.
// Evita agregar dependencia AspNetCore.HealthChecks.SqlServer (regla 8).
public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly SqlConnectionFactory _factory;

    public SqlServerHealthCheck(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var con = _factory.Crear();
            await con.OpenAsync(ct);
            await using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy("SQL Server responde.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server no responde.", ex);
        }
    }
}
