using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface ISourceRecommendationService
{
    /// <summary>
    /// キーワードに基づいてAIがソースを推薦
    /// </summary>
    /// <param name="keyword">検索キーワード</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>推薦されたソースのリスト（各ソースのRecommendationReasonに理由が設定される）</returns>
    Task<SourceRecommendationResult> RecommendSourcesAsync(string keyword, CancellationToken cancellationToken = default);
}

/// <summary>
/// ソース推薦の結果
/// </summary>
public class SourceRecommendationResult
{
    /// <summary>推薦されたソースリスト（RecommendationReasonが設定済み）</summary>
    public List<Source> RecommendedSources { get; set; } = new();

    /// <summary>キーワード分析結果</summary>
    public string? KeywordAnalysis { get; set; }

    /// <summary>検出された言語</summary>
    public string? DetectedLanguage { get; set; }

    /// <summary>検出されたカテゴリ</summary>
    public string? DetectedCategory { get; set; }

    /// <summary>AIが生成した英語エイリアス（カンマ区切り）</summary>
    public string? EnglishAliases { get; set; }
}
