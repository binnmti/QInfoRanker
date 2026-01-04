using QInfoRanker.Core.Entities;

namespace QInfoRanker.Core.Interfaces.Services;

public interface ICollectionService
{
    /// <summary>
    /// 全アクティブキーワードを収集
    /// </summary>
    Task CollectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 特定キーワードを収集（デフォルト設定）
    /// </summary>
    Task CollectForKeywordAsync(int keywordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 特定キーワードを収集（デバッグモード対応）
    /// </summary>
    /// <param name="keywordId">キーワードID</param>
    /// <param name="debugMode">デバッグモード（各ソース少数記事のみ）</param>
    /// <param name="debugArticleLimit">デバッグモード時の各ソースからの記事上限</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    Task CollectForKeywordAsync(int keywordId, bool debugMode, int debugArticleLimit = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// 特定ソースから記事を収集
    /// </summary>
    Task<IEnumerable<Article>> CollectFromSourceAsync(Source source, string keyword, DateTime? since = null, CancellationToken cancellationToken = default);
}
