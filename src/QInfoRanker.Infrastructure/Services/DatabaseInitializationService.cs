using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using QInfoRanker.Infrastructure.Data;

namespace QInfoRanker.Infrastructure.Services;

/// <summary>
/// データベース初期化をバックグラウンドで実行するHostedService。
/// アプリケーション起動をブロックせずにマイグレーションとシードを実行する。
/// </summary>
public class DatabaseInitializationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializationService> _logger;

    /// <summary>
    /// データベース初期化が完了したかどうかを示すフラグ。
    /// ヘルスチェックなどで参照可能。
    /// </summary>
    /// <remarks>
    /// volatile修飾子により、マルチスレッド環境での可視性を保証。
    /// </remarks>
    private static volatile bool _isInitialized;
    public static bool IsInitialized => _isInitialized;

    /// <summary>
    /// 初期化中にエラーが発生したかどうかを示すフラグ。
    /// </summary>
    /// <remarks>
    /// セキュリティ上、エラー詳細は公開せずフラグのみ。詳細はログに記録される。
    /// </remarks>
    private static volatile bool _hasError;
    public static bool HasError => _hasError;

    public DatabaseInitializationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DatabaseInitializationService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting database initialization in background...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var seedSampleData = _configuration.GetValue<bool>("SeedSampleData");

            // マイグレーションとシードをバックグラウンドで実行
            await DbSeeder.SeedAsync(context, seedSampleData, stoppingToken);

            _isInitialized = true;
            _logger.LogInformation("Database initialization completed successfully.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // シャットダウン時のキャンセルは正常終了として扱う
            _logger.LogInformation("Database initialization was cancelled due to application shutdown.");
        }
        catch (Exception ex)
        {
            _hasError = true;
            _logger.LogError(ex, "Failed to initialize database. Error: {Message}", ex.Message);

            // 致命的なエラーではないので、アプリケーションは継続
            // ただし、DB操作は失敗する可能性がある
        }
    }
}
