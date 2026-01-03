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
                options.UseSqlServer(connectionString));
        }

        // Configuration
        services.Configure<AzureOpenAIOptions>(configuration.GetSection(AzureOpenAIOptions.SectionName));
        services.Configure<ScoringOptions>(configuration.GetSection(ScoringOptions.SectionName));
        services.Configure<BatchScoringOptions>(configuration.GetSection(BatchScoringOptions.SectionName));

        // HttpClient for collectors
        services.AddHttpClient<HackerNewsCollector>();
        services.AddHttpClient<ArXivCollector>();
        services.AddHttpClient<RedditCollector>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "QInfoRanker/1.0");
        });
        services.AddHttpClient<HatenaCollector>();
        services.AddHttpClient<QiitaCollector>();
        services.AddHttpClient<ZennCollector>();
        // 新規コレクター
        services.AddHttpClient<GoogleNewsCollector>();
        services.AddHttpClient<PubMedCollector>();
        services.AddHttpClient<NoteCollector>();
        services.AddHttpClient<SemanticScholarCollector>();
        services.AddHttpClient<BBCNewsCollector>();
        services.AddHttpClient<YahooNewsJapanCollector>();

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
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<IScoringService, ScoringService>();
        services.AddScoped<ISourceRecommendationService, SourceRecommendationService>();

        // Background collection queue (Singleton for shared state)
        services.AddSingleton<ICollectionQueue, CollectionQueue>();
        services.AddHostedService<CollectionBackgroundService>();

        return services;
    }
}
