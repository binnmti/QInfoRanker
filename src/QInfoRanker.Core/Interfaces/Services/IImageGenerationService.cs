namespace QInfoRanker.Core.Interfaces.Services;

/// <summary>
/// 画像生成サービスのインターフェース
/// </summary>
public interface IImageGenerationService
{
    /// <summary>
    /// 週次サマリーのタイトルと内容から画像を生成し、Blob Storageに保存する
    /// </summary>
    /// <param name="summaryTitle">週次サマリーのタイトル</param>
    /// <param name="summaryContent">週次サマリーの内容</param>
    /// <param name="keywordTerm">キーワード</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>生成された画像のURL（Blob Storage）。生成できない場合はnull</returns>
    Task<string?> GenerateAndUploadImageAsync(
        string summaryTitle,
        string summaryContent,
        string keywordTerm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 画像生成が有効かどうかを確認する
    /// </summary>
    bool IsEnabled { get; }
}
