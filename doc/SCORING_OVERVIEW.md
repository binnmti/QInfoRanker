# QInfoRanker 収集・スコアリング概要

## 用語定義

| 用語 | 説明 |
|------|------|
| **Filtering** | 関連性フィルタリング（0-10点で判定、閾値未満を除外） |
| **Ensemble** | 本評価（単一の高性能モデルによる5軸評価） |

---

## アーキテクチャ（v2: 統一評価方式）

### 設計思想

- **シンプル**: 複数Judge + MetaJudgeから、単一モデルによる統一評価へ
- **コスト効率**: API呼び出し回数を大幅削減（旧: 3-5回/記事 → 新: 1回/記事）
- **精度維持**: 高性能モデル1回の評価は、低性能モデル複数回の評価と同等以上の精度

---

## 全体フロー図

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         キーワード「AI」で収集開始                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  【収集】各ソース（HackerNews, ArXiv, Qiita等）から記事を取得                  │
│  例: 100件の記事を収集                                                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  【Stage 1: Filtering】関連性フィルタリング                                   │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │  設定: BatchScoring.Filtering                                         │  │
│  │  ├── DeploymentName: gpt-5-nano     ← 高速・低コストモデル             │  │
│  │  └── BatchSize: 15                  ← 15件ずつバッチ処理              │  │
│  │                                                                       │  │
│  │  処理: タイトル＋サマリーで関連性を0-10で簡易判定                        │  │
│  │  目的: 明らかに無関係な記事を高速に除外（コスト削減）                     │  │
│  │                                                                       │  │
│  │  閾値: FilteringPreset                                                │  │
│  │  ├── Loose: 2.0（幅広く通過）                                         │  │
│  │  ├── Normal: 3.0（デフォルト）                                        │  │
│  │  └── Strict: 6.0（厳選）                                              │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│  結果: 100件 → 35件 に絞り込み（65%削減）                                    │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  【Stage 2: Ensemble】本評価（統一モデル）                                    │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │  設定: EnsembleScoring                                                │  │
│  │  ├── DeploymentName: o3-mini        ← 高品質な推論モデル               │  │
│  │  └── BatchSize: 5                   ← 5件ずつバッチ処理               │  │
│  │                                                                       │  │
│  │  処理: 1記事ずつ5軸評価（バッチサイズ分を順次処理）                      │  │
│  │  モデル: 単一の高性能モデルで統一評価                                   │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  評価項目（各0-20点、合計100点満点）:                                         │
│  ├── relevance: 最終関連性（EnsembleRelevanceScore）                         │
│  ├── technical: 技術的深さ                                                  │
│  ├── novelty: 新規性                                                        │
│  ├── impact: 実用性                                                         │
│  └── quality: 情報の質                                                      │
│                                                                             │
│  追加出力: 日本語要約（250-400文字）                                          │
│  除外条件: relevance < EnsembleRelevanceThreshold (デフォルト: 6)             │
└─────────────────────────────────────────────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  【最終スコア】                                                              │
│  FinalScore = relevance + technical + novelty + impact + quality            │
│             = 0-20 + 0-20 + 0-20 + 0-20 + 0-20 = 0-100点                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 設定ファイル（appsettings.json）

### 推奨構成

```json
{
  "Scoring": {
    "Preset": "QualityFocused",
    "EnsembleRelevanceThreshold": 6
  },
  "BatchScoring": {
    "FilteringPreset": "Normal",
    "Filtering": {
      "DeploymentName": "gpt-5-nano"
    }
  },
  "EnsembleScoring": {
    "DeploymentName": "o3-mini",
    "BatchSize": 5
  },
  "WeeklySummary": {
    "DeploymentName": "o3-mini"
  }
}
```

### コスト試算（100記事/日、30日運用）

| 項目 | モデル | API呼び出し | 月額目安 |
|------|--------|-------------|---------|
| Filtering | gpt-5-nano | 〜7回/日 | 〜$0.5 |
| Ensemble | o3-mini | 〜35回/日 | 〜$3 |
| **合計** | - | - | **〜$3.5/月** |

※ 旧アーキテクチャ（複数Judge + MetaJudge）では〜$10-30/月

---

## 設定項目リファレンス

### Scoring

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `Preset` | 任意 | QualityFocused | スコアリングプリセット |
| `EnsembleRelevanceThreshold` | 任意 | 6 | 本評価での関連性閾値（0-20点） |

### BatchScoring

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `FilteringPreset` | 任意 | Normal | フィルタリング閾値プリセット |
| `Filtering.DeploymentName` | **必須** | - | フィルタリング用モデル |

### EnsembleScoring

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `DeploymentName` | **必須** | o3-mini | 本評価用モデル（推論モデル推奨） |
| `BatchSize` | 任意 | 5 | バッチ処理の記事数 |
| `TimeoutMs` | 任意 | 120000 | タイムアウト（ミリ秒） |

### WeeklySummary

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `DeploymentName` | 任意 | o3-mini | 週次まとめ用モデル |

---

## モデル選定ガイド

### 用途別推奨

| 用途 | 推奨モデル | 理由 |
|------|-----------|------|
| Filtering | gpt-5-nano | 高速・低コスト。0-10判定には十分 |
| Ensemble | o3-mini | 推論モデル。5軸評価に適している |
| WeeklySummary | o3-mini | 推論モデル。長文の統合・要約に適している |

### パラメータ互換性

| モデル分類 | Temperature | MaxTokens | 備考 |
|-----------|:-----------:|:---------:|------|
| 推論モデル（o3, gpt-5系） | ✗ | ✗ | SDK側で自動処理 |
| 通常モデル（gpt-4o系） | ○ | ○ | 明示的に設定可能 |

※ `ModelCapabilities.cs` でモデル名から自動判定

---

## プロンプト概要

### Filtering（関連性フィルタリング）

```
キーワード「{keywords}」に対する各記事の関連性を0-10で評価。

評価基準:
- 9-10: 記事の主題がキーワードそのもの
- 7-8:  キーワードについて実質的に扱っている
- 5-6:  部分的に関連
- 3-4:  触れているが主題は別
- 0-2:  無関係

出力: {"evaluations": [{"id": 1, "relevance": 8, "reason": "..."}, ...]}
```

### Ensemble（本評価）

```
以下の技術記事を評価し、日本語で要約。

評価項目（各0-20点）:
- relevance: キーワードとの関連性
- technical: 技術的深さと重要性
- novelty: 新規性・独自性
- impact: 実用的な影響度
- quality: 情報の質と信頼性

出力:
{
  "relevance": 16, "relevance_reason": "...",
  "technical": 18, "technical_reason": "...",
  "novelty": 15, "novelty_reason": "...",
  "impact": 16, "impact_reason": "...",
  "quality": 17, "quality_reason": "...",
  "total": 82,
  "summary_ja": "新しいAI機能のAPIが公開..."
}
```

---

## Articleエンティティのスコアフィールド

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `RelevanceScore` | double? | Filtering時の関連性スコア（0-10） |
| `EnsembleRelevanceScore` | int? | 本評価での関連性スコア（0-20） |
| `TechnicalScore` | int? | 技術評価スコア（0-20） |
| `NoveltyScore` | int? | 新規性スコア（0-20） |
| `ImpactScore` | int? | 影響度スコア（0-20） |
| `QualityScore` | int? | 品質スコア（0-20） |
| `LlmScore` | double? | 合計スコア（0-100） |
| `FinalScore` | double | 最終スコア（重み付け適用後） |

---

## 関連ファイル

| 処理 | ファイル |
|------|---------|
| 収集フロー | `Infrastructure/Services/CollectionService.cs` |
| スコアリング | `Infrastructure/Scoring/ScoringService.cs` |
| バッチ設定 | `Infrastructure/Scoring/BatchScoringOptions.cs` |
| スコアリング設定 | `Infrastructure/Scoring/ScoringOptions.cs` |
| 本評価設定 | `Infrastructure/Scoring/EnsembleScoringOptions.cs` |
| 週次まとめ設定 | `Infrastructure/Scoring/WeeklySummaryOptions.cs` |
| 週次まとめサービス | `Infrastructure/Services/WeeklySummaryService.cs` |
| 記事エンティティ | `Core/Entities/Article.cs` |

---

## 変更履歴

### v2（現在）
- 複数Judge + MetaJudgeから単一モデル統一評価へ移行
- API呼び出し回数を大幅削減（3-5回/記事 → 1回/記事）
- コストを約70%削減
- 精度は同等以上を維持

### v1（旧）
- 複数のJudgeモデルで並列評価
- MetaJudgeで評価を統合
- コストが高く、複雑な構成
