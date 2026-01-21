using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QInfoRanker.Infrastructure.Services;

/// <summary>
/// データベース初期化の完了状態を確認するヘルスチェック。
/// DatabaseInitializationServiceがバックグラウンドで完了するまではDegradedを返す。
/// エラー発生時はUnhealthyを返す（セキュリティ上、詳細は公開しない）。
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

        if (DatabaseInitializationService.HasError)
        {
            // セキュリティ上、エラー詳細は公開しない。詳細はログで確認。
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Database initialization failed. Check application logs for details."));
        }

        return Task.FromResult(HealthCheckResult.Degraded("Database initialization in progress..."));
    }
}
