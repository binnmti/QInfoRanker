using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Images;
using QInfoRanker.Core.Interfaces.Services;
using QInfoRanker.Infrastructure.Scoring;

namespace QInfoRanker.Infrastructure.Services;

/// <summary>
/// DALL-E 3を使用して画像を生成し、Azure Blob Storageに保存するサービス
/// </summary>
public class ImageGenerationService : IImageGenerationService
{
    private readonly AzureOpenAIOptions _openAIOptions;
    private readonly SummaryImageOptions _imageOptions;
    private readonly BlobStorageOptions _blobOptions;
    private readonly ILogger<ImageGenerationService> _logger;
    private readonly ImageClient? _imageClient;
    private readonly BlobContainerClient? _containerClient;

    public ImageGenerationService(
        IOptions<AzureOpenAIOptions> openAIOptions,
        IOptions<SummaryImageOptions> imageOptions,
        IOptions<BlobStorageOptions> blobOptions,
        ILogger<ImageGenerationService> logger)
    {
        _openAIOptions = openAIOptions.Value;
        _imageOptions = imageOptions.Value;
        _blobOptions = blobOptions.Value;
        _logger = logger;

        // Azure OpenAI クライアントの初期化
        // 画像生成用の設定があればそちらを優先、なければメインの設定を使用
        var endpoint = !string.IsNullOrEmpty(_imageOptions.Endpoint) ? _imageOptions.Endpoint : _openAIOptions.Endpoint;
        var apiKey = !string.IsNullOrEmpty(_imageOptions.ApiKey) ? _imageOptions.ApiKey : _openAIOptions.ApiKey;

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            try
            {
                var credential = new ApiKeyCredential(apiKey);
                var client = new AzureOpenAIClient(new Uri(endpoint), credential);
                _imageClient = client.GetImageClient(_imageOptions.DeploymentName);
                _logger.LogInformation("Image generation client initialized with endpoint: {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Azure OpenAI Image client");
            }
        }

        // Blob Storage クライアントの初期化
        if (_blobOptions.IsConfigured)
        {
            try
            {
                _containerClient = new BlobContainerClient(_blobOptions.ConnectionString, _blobOptions.ContainerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Blob Storage client");
            }
        }
    }

    public bool IsEnabled =>
        _imageOptions.Enabled &&
        _imageClient != null &&
        _containerClient != null;

    public async Task<string?> GenerateAndUploadImageAsync(
        string summaryTitle,
        string summaryContent,
        string keywordTerm,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("Image generation is disabled or not configured");
            return null;
        }

        try
        {
            // 画像生成用プロンプトを作成（サマリー内容から主要トピックを抽出）
            var prompt = CreateImagePrompt(summaryTitle, summaryContent, keywordTerm);

            // DALL-E 3で画像を生成
            var imageBytes = await GenerateImageAsync(prompt, cancellationToken);
            if (imageBytes == null)
            {
                return null;
            }

            // Blob Storageにアップロード
            var imageUrl = await UploadToBlobAsync(imageBytes, keywordTerm, cancellationToken);

            _logger.LogInformation("Generated and uploaded image for '{Keyword}': {Url}", keywordTerm, imageUrl);
            return imageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate or upload image for '{Keyword}'", keywordTerm);
            return null;
        }
    }

    private static string CreateImagePrompt(string summaryTitle, string summaryContent, string keywordTerm)
    {
        // サマリー内容から主要トピックを抽出（見出しと重要なキーワード）
        var keyTopics = ExtractKeyTopics(summaryContent);

        // グラフィカルアブストラクト / ビジュアルサマリー スタイルのプロンプト
        return $"""
            Create a graphical abstract (visual summary) for a weekly technology digest about "{keywordTerm}".

            Summary title: {summaryTitle}
            Key topics to visualize: {keyTopics}

            IMPORTANT - Graphical Abstract Style Requirements:
            - Create a structured, panel-style infographic layout
            - Use clean icons, simple diagrams, and visual metaphors to represent each key topic
            - Show relationships and connections between concepts using arrows or flow lines
            - Organize information in a clear visual hierarchy (top-to-bottom or left-to-right flow)
            - Use a consistent color scheme with 3-4 main colors
            - Include simple pictograms representing: technology, data, innovation, research
            - White or light gradient background for clarity
            - Professional scientific/technical illustration style
            - NO text, letters, numbers, or words anywhere in the image
            - Each concept should be represented by a distinct visual element or icon

            Visual elements to include:
            - Central theme icon representing "{keywordTerm}"
            - 3-4 connected sub-elements representing the key topics
            - Connecting lines or arrows showing relationships
            - Clean geometric shapes and modern flat design icons

            Style: Scientific graphical abstract, infographic, flat design icons, clean vector-style illustration
            """;
    }

    private static string ExtractKeyTopics(string summaryContent)
    {
        // Markdown見出し（## で始まる行）を抽出
        var headings = new List<string>();
        var lines = summaryContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("## "))
            {
                headings.Add(trimmed[3..].Trim());
            }
        }

        // 見出しがあればそれを使用、なければサマリーの最初の部分を使用
        if (headings.Count > 0)
        {
            return string.Join(", ", headings.Take(3));
        }

        // 見出しがない場合は最初の200文字を要約として使用
        var plainText = summaryContent
            .Replace("#", "")
            .Replace("*", "")
            .Replace("[", "")
            .Replace("]", "")
            .Replace("(", "")
            .Replace(")", "");

        return plainText.Length > 200 ? plainText[..200] + "..." : plainText;
    }

    private async Task<byte[]?> GenerateImageAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_imageClient == null)
        {
            return null;
        }

        var size = _imageOptions.Size switch
        {
            "1792x1024" => GeneratedImageSize.W1792xH1024,
            "1024x1792" => GeneratedImageSize.W1024xH1792,
            _ => GeneratedImageSize.W1024xH1024
        };

        var quality = _imageOptions.Quality switch
        {
            "hd" => GeneratedImageQuality.High,
            _ => GeneratedImageQuality.Standard
        };

        var style = _imageOptions.Style switch
        {
            "natural" => GeneratedImageStyle.Natural,
            _ => GeneratedImageStyle.Vivid
        };

        var options = new ImageGenerationOptions
        {
            Size = size,
            Quality = quality,
            Style = style,
            ResponseFormat = GeneratedImageFormat.Bytes
        };

        try
        {
            var result = await _imageClient.GenerateImageAsync(prompt, options, cancellationToken);
            return result.Value.ImageBytes?.ToArray();
        }
        catch (ClientResultException ex)
        {
            _logger.LogWarning(ex, "DALL-E 3 image generation failed: {Message}", ex.Message);
            return null;
        }
    }

    private async Task<string> UploadToBlobAsync(byte[] imageBytes, string keywordTerm, CancellationToken cancellationToken)
    {
        if (_containerClient == null)
        {
            throw new InvalidOperationException("Blob container client is not initialized");
        }

        // コンテナが存在しない場合は作成
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        // ファイル名を生成（キーワード + タイムスタンプ）
        var sanitizedKeyword = SanitizeFileName(keywordTerm);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var blobName = $"{sanitizedKeyword}/{timestamp}.png";

        var blobClient = _containerClient.GetBlobClient(blobName);

        // アップロード
        using var stream = new MemoryStream(imageBytes);
        await blobClient.UploadAsync(
            stream,
            new BlobHttpHeaders { ContentType = "image/png" },
            cancellationToken: cancellationToken);

        return blobClient.Uri.ToString();
    }

    private static string SanitizeFileName(string input)
    {
        // ファイル名に使用できない文字を置換
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", input.Select(c => invalidChars.Contains(c) ? '_' : c));
        return sanitized.ToLowerInvariant().Replace(' ', '-');
    }
}
