using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QInfoRanker.Infrastructure.Services;

/// <summary>
/// データベース初期化の完了状態を確認するヘルスチェック。
/// DatabaseInitializationServiceがバックグラウンドで完了するまではDegradedを返す。
/// </summary>
public class DatabaseInitializationHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (DatabaseInitializationService.IsInitialized)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Database initialization completed."));
        }

        if (!string.IsNullOrEmpty(DatabaseInitializationService.InitializationError))
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Database initialization failed: {DatabaseInitializationService.InitializationError}"));
        }

        return Task.FromResult(HealthCheckResult.Degraded("Database initialization in progress..."));
    }
}
