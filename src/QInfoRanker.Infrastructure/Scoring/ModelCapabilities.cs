namespace QInfoRanker.Infrastructure.Scoring;

/// <summary>
/// Azure OpenAI モデルの機能・パラメータサポート情報を管理
///
/// 参照: https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/reasoning
///
/// 推論モデル (Reasoning Models) は以下のパラメータをサポートしない:
/// - temperature, top_p, presence_penalty, frequency_penalty
/// - max_tokens (代わりに max_completion_tokens を使用)
/// - logprobs, top_logprobs, logit_bias
///
/// 設計方針:
/// - Temperature/MaxTokens は設定ファイルで指定せず、DeploymentName から自動判定
/// - 通常モデルにはデフォルト値を使用（必要に応じてオーバーライド可能）
/// - 推論モデルにはこれらのパラメータを一切設定しない
/// </summary>
public static class ModelCapabilities
{
    /// <summary>
    /// 推論モデルのデプロイメント名パターン
    /// これらのモデルは temperature/max_tokens をサポートしない
    /// </summary>
    private static readonly string[] ReasoningModelPrefixes = new[]
    {
        // O-series reasoning models
        "o1",           // o1, o1-mini, o1-preview
        "o3",           // o3, o3-mini, o3-pro
        "o4",           // o4-mini
        "codex-mini",   // codex-mini (reasoning version)

        // GPT-5 series (all are reasoning models)
        "gpt-5",        // gpt-5, gpt-5-mini, gpt-5-nano, gpt-5-codex, gpt-5-pro, gpt-5-chat
                        // gpt-5.1, gpt-5.1-chat, gpt-5.1-codex, gpt-5.1-codex-mini, gpt-5.1-codex-max, gpt-5.2
    };

    /// <summary>
    /// 非推論モデル (通常モデル) のパターン
    /// これらは temperature/max_tokens をサポートする
    /// </summary>
    private static readonly string[] NonReasoningModelPrefixes = new[]
    {
        "gpt-4o",       // gpt-4o, gpt-4o-mini
        "gpt-4",        // gpt-4, gpt-4-turbo, gpt-4-32k
        "gpt-3.5",      // gpt-3.5-turbo
        "gpt-35",       // Azure naming variant
        "text-",        // text-davinci, text-embedding
        "davinci",
        "curie",
        "babbage",
        "ada"
    };

    /// <summary>
    /// 指定されたデプロイメント名が推論モデルかどうかを判定
    /// </summary>
    /// <param name="deploymentName">Azure OpenAI のデプロイメント名</param>
    /// <returns>推論モデルの場合 true</returns>
    public static bool IsReasoningModel(string deploymentName)
    {
        if (string.IsNullOrWhiteSpace(deploymentName))
            return false;

        var normalized = deploymentName.ToLowerInvariant().Trim();

        // まず非推論モデルをチェック（より具体的なマッチ）
        foreach (var prefix in NonReasoningModelPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 推論モデルパターンをチェック
        foreach (var prefix in ReasoningModelPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 不明なモデルはデフォルトで非推論モデルとして扱う
        return false;
    }

    /// <summary>
    /// Temperature パラメータをサポートするか
    /// </summary>
    public static bool SupportsTemperature(string deploymentName)
        => !IsReasoningModel(deploymentName);

    /// <summary>
    /// max_tokens パラメータをサポートするか
    /// 推論モデルは max_completion_tokens を使用する必要がある
    /// </summary>
    public static bool SupportsMaxTokens(string deploymentName)
        => !IsReasoningModel(deploymentName);

    /// <summary>
    /// max_completion_tokens パラメータを使用すべきか
    /// 推論モデルはこちらを使用する
    /// </summary>
    public static bool ShouldUseMaxCompletionTokens(string deploymentName)
        => IsReasoningModel(deploymentName);

    /// <summary>
    /// top_p パラメータをサポートするか
    /// </summary>
    public static bool SupportsTopP(string deploymentName)
        => !IsReasoningModel(deploymentName);

    /// <summary>
    /// presence_penalty/frequency_penalty をサポートするか
    /// </summary>
    public static bool SupportsPenaltyParameters(string deploymentName)
        => !IsReasoningModel(deploymentName);

    /// <summary>
    /// reasoning_effort パラメータをサポートするか
    /// </summary>
    public static bool SupportsReasoningEffort(string deploymentName)
        => IsReasoningModel(deploymentName);

    /// <summary>
    /// モデルの機能サマリを取得（デバッグ・ログ用）
    /// </summary>
    public static ModelCapabilitySummary GetCapabilitySummary(string deploymentName)
    {
        var isReasoning = IsReasoningModel(deploymentName);
        return new ModelCapabilitySummary
        {
            DeploymentName = deploymentName,
            IsReasoningModel = isReasoning,
            SupportsTemperature = !isReasoning,
            SupportsMaxTokens = !isReasoning,
            SupportsTopP = !isReasoning,
            SupportsPenalties = !isReasoning,
            SupportsReasoningEffort = isReasoning,
            TokenLimitParameterName = isReasoning ? "max_completion_tokens" : "max_tokens"
        };
    }

    #region デフォルトパラメータ値

    /// <summary>
    /// 通常モデルのデフォルト Temperature
    /// 推論モデルには適用されない（設定自体が不可）
    /// </summary>
    public const float DefaultTemperature = 0.3f;

    /// <summary>
    /// 通常モデルのデフォルト MaxTokens
    /// 推論モデルには適用されない（設定自体が不可）
    /// </summary>
    public const int DefaultMaxTokens = 4000;

    /// <summary>
    /// 指定されたモデルに適用すべき Temperature を取得
    /// 推論モデルの場合は null を返す（パラメータ設定不可のため）
    /// </summary>
    /// <param name="deploymentName">デプロイメント名</param>
    /// <param name="overrideValue">オーバーライド値（通常モデルのみ有効）</param>
    /// <returns>設定すべき温度値、または null（設定不可の場合）</returns>
    public static float? GetEffectiveTemperature(string deploymentName, float? overrideValue = null)
    {
        if (IsReasoningModel(deploymentName))
            return null; // 推論モデルは Temperature 設定不可

        return overrideValue ?? DefaultTemperature;
    }

    /// <summary>
    /// 指定されたモデルに適用すべき MaxTokens を取得
    /// 推論モデルの場合は null を返す（パラメータ設定不可のため）
    /// </summary>
    /// <param name="deploymentName">デプロイメント名</param>
    /// <param name="overrideValue">オーバーライド値（通常モデルのみ有効、0以下は無効）</param>
    /// <returns>設定すべきトークン数、または null（設定不可の場合）</returns>
    public static int? GetEffectiveMaxTokens(string deploymentName, int? overrideValue = null)
    {
        if (IsReasoningModel(deploymentName))
            return null; // 推論モデルは MaxTokens 設定不可

        if (overrideValue.HasValue && overrideValue.Value > 0)
            return overrideValue.Value;

        return DefaultMaxTokens;
    }

    #endregion
}

/// <summary>
/// モデル機能のサマリ情報
/// </summary>
public class ModelCapabilitySummary
{
    public string DeploymentName { get; set; } = string.Empty;
    public bool IsReasoningModel { get; set; }
    public bool SupportsTemperature { get; set; }
    public bool SupportsMaxTokens { get; set; }
    public bool SupportsTopP { get; set; }
    public bool SupportsPenalties { get; set; }
    public bool SupportsReasoningEffort { get; set; }
    public string TokenLimitParameterName { get; set; } = "max_tokens";

    public override string ToString()
    {
        var type = IsReasoningModel ? "Reasoning" : "Standard";
        return $"{DeploymentName} ({type}): temp={SupportsTemperature}, maxTokens={SupportsMaxTokens}, reasoning={SupportsReasoningEffort}";
    }
}
