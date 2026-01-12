# 収集ロジック概要

このドキュメントでは、QInfoRankerの記事収集・スコアリングの仕組みを説明します。

## 1. 収集フロー全体像

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         収集トリガー                                      │
│   ・手動: Webアプリの「収集」ボタン                                        │
│   ・自動: Container Apps Job（毎日06:00 JST）                             │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  1. 記事収集（Collector）                                                │
│     ・各ソース（HackerNews, ArXiv, Qiita等）から記事を取得                  │
│     ・キーワード + エイリアスで検索                                        │
│     ・過去1ヶ月分を対象                                                   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  2. 重複チェック（ArticleService.CreateBatchAsync）  ★コスト削減ポイント   │
│     ・同じURL → スキップ                                                 │
│     ・同じタイトル → スキップ                                             │
│     ・新規記事のみDB保存 → スコアリング対象                                │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                        ┌───────────┴───────────┐
                        │                       │
                   既存記事                  新規記事
                   （スキップ）              （スコアリングへ）
                                                │
                                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  3. Filtering（関連性フィルタリング）  ★AI呼び出し①                       │
│     ・モデル: gpt-5-nano（軽量・低コスト）                                 │
│     ・判定: キーワードとの関連性を0-10点で評価                             │
│     ・閾値: 3.0未満は除外（FilteringPreset=Normalの場合）                  │
│     ・目的: 無関係な記事を早期に除外してコスト削減                          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                        ┌───────────┴───────────┐
                        │                       │
                   閾値未満                  閾値以上
                 （除外・低スコア）          （本評価へ）
                                                │
                                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  4. Ensemble（本評価）  ★AI呼び出し②                                    │
│     ・モデル: o3-mini（高性能）                                           │
│     ・5軸評価（各0-20点、合計100点満点）:                                  │
│       - relevance: 関連性（6未満は最終的に除外）                           │
│       - technical: 技術的深さ                                            │
│       - novelty: 新規性                                                  │
│       - impact: 実用性                                                   │
│       - quality: 情報の質                                                │
│     ・追加: recommend（おすすめ度）、summary_ja（日本語要約）               │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  5. 最終スコア計算 & DB保存                                               │
│     ・FinalScore = (NativeScore × 0.3 + LlmScore × 0.7) × AuthorityBonus │
│     ・ランキングページに反映                                              │
└─────────────────────────────────────────────────────────────────────────┘
```

## 2. 収集対象期間

### 2.1 現在の設定

| 項目 | 値 |
|-----|-----|
| **期間** | 過去1ヶ月（`AddMonths(-1)`） |
| **起点** | 収集実行時点（`DateTime.UtcNow`） |

```csharp
// CollectionService.cs より
var since = DateTime.UtcNow.AddMonths(-1);
```

### 2.2 ソースごとの期間フィルタリング

各Collectorは`since`パラメータを使って期間フィルタリングを行います。

**HackerNews**: APIパラメータで期間指定
```csharp
var timestamp = new DateTimeOffset(since.Value).ToUnixTimeSeconds();
searchUrl += $"&numericFilters=created_at_i>{timestamp}";
```

**ArXiv**: 取得後にフィルタリング
```csharp
if (since.HasValue && publishedDate < since.Value)
    continue;  // スキップ
```

### 2.3 毎日収集した場合のイメージ

```
1月1日に初回収集
  → 12月1日〜1月1日の記事を収集（1ヶ月分）
  → 例: 100件収集 → 100件AI処理

1月2日に収集
  → 12月2日〜1月2日の記事を収集（1ヶ月分）
  → 例: 100件収集 → 重複チェックで95件スキップ → 5件AI処理（新規のみ）

1月3日に収集
  → 12月3日〜1月3日の記事を収集（1ヶ月分）
  → 例: 100件収集 → 重複チェックで97件スキップ → 3件AI処理（新規のみ）
```

**ポイント**: 毎回1ヶ月分を取得しますが、**重複チェックで既存記事はスキップ**されるため、AIコストは「新規公開分」のみです。

## 3. 重複チェックの仕組み（コスト削減の要）

### 3.1 チェック対象

```csharp
// ArticleService.CreateBatchAsync より抜粋
var existingUrls = existingArticles.Select(a => a.Url).ToHashSet();
var existingTitles = existingArticles.Select(a => a.Title.ToLowerInvariant()).ToHashSet();

var newArticles = articleList
    .Where(a => !existingUrls.Contains(a.Url) &&
               !existingTitles.Contains(a.Title.ToLowerInvariant()))
    .ToList();
```

### 3.2 重複チェックの効果

| シナリオ | 収集記事数 | AI呼び出し対象 |
|---------|-----------|---------------|
| 初回収集 | 100件 | 100件（全て新規） |
| 翌日収集 | 100件 | 10件（新規のみ） |
| 1週間後 | 100件 | 5件（新規のみ） |

**ポイント**: 毎日収集しても、**新しい記事だけ**がAI処理対象になる

## 4. AI呼び出しコストの内訳

### 4.1 Filtering（関連性フィルタリング）

| 項目 | 値 |
|-----|-----|
| モデル | gpt-5-nano（軽量） |
| 呼び出しタイミング | 新規記事ごと |
| 入力 | 記事タイトル + 概要（短い） |
| 出力 | 関連性スコア（0-10） |
| コスト目安 | 低（軽量モデル使用） |

### 4.2 Ensemble（本評価）

| 項目 | 値 |
|-----|-----|
| モデル | o3-mini（高性能） |
| 呼び出しタイミング | Filtering通過記事のみ |
| 入力 | 記事タイトル + 概要 + キーワード |
| 出力 | 5軸スコア + おすすめ度 + 日本語要約 |
| コスト目安 | 中〜高（高性能モデル使用） |

### 4.3 コスト削減の仕組み

```
収集記事 100件
    │
    ▼ 重複チェック（AI呼び出しなし）
新規記事 20件
    │
    ▼ Filtering（軽量AI × 20回）
関連記事 10件
    │
    ▼ Ensemble（高性能AI × 10回）
最終スコアリング完了
```

**削減効果**:
- 重複チェックで 80% 削減（100→20件）
- Filteringで 50% 削減（20→10件）
- **結果: 高性能AIは10回のみ呼び出し**

## 5. 定期収集（Container Apps Job）

### 5.1 動作概要

```
毎日 06:00 JST（UTC 21:00）
    │
    ▼
Worker起動
    │
    ▼
有効なキーワード（IsActive=true）を全て取得
    │
    ▼
各キーワードに対して CollectForKeywordAsync 実行
    │
    ▼
Worker終了
```

### 5.2 設定（infra/variables.tf）

```hcl
variable "collection_schedule" {
  description = "Cron expression for collection job schedule (UTC)"
  type        = string
  default     = "0 21 * * *"  # 06:00 JST
}
```

### 5.3 手動収集との違い

| 項目 | 手動収集 | 定期収集 |
|-----|---------|---------|
| トリガー | UIの「収集」ボタン | Container Apps Jobのスケジュール |
| 対象 | 選択したキーワード | 全ての有効キーワード |
| 実行環境 | App Service（Webアプリ内） | Container Apps（独立コンテナ） |
| ロジック | 同じ（CollectionService） | 同じ（CollectionService） |

## 6. コスト最適化のヒント

### 6.1 現在実装済みの最適化

1. **重複チェック**: URL・タイトルで既存記事をスキップ
2. **2段階フィルタリング**: 軽量モデルで事前選別
3. **バッチ処理**: 複数記事をまとめてAPI呼び出し

### 6.2 追加で検討できる最適化

| 最適化案 | 効果 | 実装難易度 |
|---------|------|-----------|
| 収集期間の短縮（1ヶ月→1週間） | 収集記事数減少 | 低 |
| Filteringプリセットを厳格に | 本評価対象減少 | 低（設定変更のみ） |
| 収集頻度の調整（毎日→週3回） | 呼び出し回数減少 | 低（cron変更のみ） |
| 記事の鮮度チェック追加 | 古い記事をスキップ | 中 |

### 6.3 設定によるコスト調整

**FilteringPreset**（`appsettings.json`）:
- `Loose`: 閾値2.0（多くの記事が本評価へ → コスト高）
- `Normal`: 閾値3.0（バランス型）
- `Strict`: 閾値6.0（厳格 → コスト低）

```json
{
  "BatchScoring": {
    "FilteringPreset": "Strict"  // コストを抑えたい場合
  }
}
```

## 7. 関連ファイル

| ファイル | 役割 |
|---------|------|
| [CollectionService.cs](../src/QInfoRanker.Infrastructure/Services/CollectionService.cs) | 収集の中心ロジック |
| [ArticleService.cs](../src/QInfoRanker.Infrastructure/Services/ArticleService.cs) | 記事の保存・重複チェック |
| [ScoringService.cs](../src/QInfoRanker.Infrastructure/Scoring/ScoringService.cs) | AIスコアリング |
| [CollectionBackgroundService.cs](../src/QInfoRanker.Infrastructure/Services/CollectionBackgroundService.cs) | バックグラウンド処理 |
| [container-job.tf](../infra/container-job.tf) | Container Apps Jobの定義 |

## 8. よくある質問

### Q1: 毎日収集するとコストが心配です

**A**: 重複チェックにより、既に収集済みの記事はAI処理されません。実際にコストがかかるのは「新しく公開された記事」のみです。

### Q2: 収集頻度を変更したい

**A**: `infra/variables.tf`の`collection_schedule`を変更してください。

```hcl
# 毎日06:00 JST
default = "0 21 * * *"

# 週3回（月水金）06:00 JST
default = "0 21 * * 1,3,5"

# 毎週月曜06:00 JST
default = "0 21 * * 1"
```

### Q3: 特定のキーワードだけ定期収集したい

**A**: キーワードの`IsActive`フラグで制御できます。WebアプリのキーワードページでON/OFFを切り替えてください。
