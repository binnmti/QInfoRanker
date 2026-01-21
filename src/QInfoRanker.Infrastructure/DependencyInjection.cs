using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QInfoRanker.Core.Interfaces.Collectors;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Collectors;
using QInfoRanker.Infrastructure.Data;
using QInfoRanker.Infrastructure.Scoring;
using QInfoRanker.Infrastructure.Services;

namespace QInfoRanker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var useSqlite = configuration.GetValue<bool>("UseSqlite");

        if (useSqlite || string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connectionString ?? "Data Source=QInfoRanker.db"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sqlServerOptions =>
                    sqlServerOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)));
        }

        // Configuration
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));
        services.Configure<ScoringOptions>(configuration.GetSection(ScoringOptions.SectionName));
        services.Configure<BatchScoringOptions>(configuration.GetSection(BatchScoringOptions.SectionName));
        services.Configure<EnsembleScoringOptions>(configuration.GetSection(EnsembleScoringOptions.SectionName));
        services.Configure<WeeklySummaryOptions>(configuration.GetSection(WeeklySummaryOptions.SectionName));
        services.Configure<SummaryImageOptions>(configuration.GetSection("ImageGeneration"));
        services.Configure<BlobStorageOptions>(configuration.GetSection("BlobStorage"));

        // HttpClient for collectors (User-Agent required to avoid bot blocking)
        const string userAgent = "QInfoRanker/1.0 (https://github.com/qinforanker; contact@qinforanker.app)";

        services.AddHttpClient<HackerNewsCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<ArXivCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<RedditCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<HatenaCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<QiitaCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<ZennCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        // 新規コレクター
        services.AddHttpClient<GoogleNewsCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<PubMedCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<NoteCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<SemanticScholarCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<BBCNewsCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });
        services.AddHttpClient<YahooNewsJapanCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        });

        // Collectors
        services.AddScoped<ICollector, HackerNewsCollector>();
        services.AddScoped<ICollector, ArXivCollector>();
        services.AddScoped<ICollector, RedditCollector>();
        services.AddScoped<ICollector, HatenaCollector>();
        services.AddScoped<ICollector, QiitaCollector>();
        services.AddScoped<ICollector, ZennCollector>();
        // 新規コレクター
        services.AddScoped<ICollector, GoogleNewsCollector>();
        services.AddScoped<ICollector, PubMedCollector>();
        services.AddScoped<ICollector, NoteCollector>();
        services.AddScoped<ICollector, SemanticScholarCollector>();
        services.AddScoped<ICollector, BBCNewsCollector>();
        services.AddScoped<ICollector, YahooNewsJapanCollector>();

        // Services
        services.AddScoped<IKeywordService, KeywordService>();
        services.AddScoped<ISourceService, SourceService>();
        services.AddScoped<IArticleService, ArticleService>();
        services.AddScoped<IScoringService, ScoringService>();
        services.AddScoped<ISourceRecommendationService, SourceRecommendationService>();
        services.AddScoped<IImageGenerationService, ImageGenerationService>();
        services.AddScoped<IWeeklySummaryService, WeeklySummaryService>();
        services.AddScoped<ICollectionService, CollectionService>();

        // Background collection queue (Singleton for shared state)
        services.AddSingleton<ICollectionQueue, CollectionQueue>();
        services.AddHostedService<CollectionBackgroundService>();

        // Progress notification service
        services.AddScoped<ICollectionProgressNotifier, CollectionProgressNotifier>();

        // Database initialization service (runs migrations and seeding in background)
        // This prevents blocking the application startup on database operations
        services.AddHostedService<DatabaseInitializationService>();

        return services;
    }
}
