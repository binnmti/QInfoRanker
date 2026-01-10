# QInfoRanker 収集・スコアリング概要

## 用語定義

| 用語 | 説明 |
|------|------|
| **Filtering** | 関連性フィルタリング（0-10点で判定、閾値未満を除外） |
| **Ensemble** | アンサンブル評価（複数Judge + MetaJudgeによる本評価） |
| **QualityFallback** | アンサンブル評価失敗時のフォールバック品質評価 |

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
│  【Filtering: 関連性フィルタリング】                                          │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │  設定: BatchScoring.Filtering                                         │  │
│  │  ├── DeploymentName: (廉価モデル)   ← 高速・低コスト                   │  │
│  │  └── BatchSize: 15                  ← 15件ずつ処理                    │  │
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
│  【Ensemble: アンサンブル評価】（常に有効）                                    │
│  設定: EnsembleScoring（記事ごとに並列評価）                                  │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                                                                       │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │ GeneralEvaluator (汎用評価)                                     │  │  │
│  │  │ ├── DeploymentName: (廉価モデル〜高性能モデル)                   │  │  │
│  │  │ ├── Weight: 1.0                                                 │  │  │
│  │  │ └── 役割: バランスの取れた総合評価                               │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  │                              │                                        │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │ TechExpert (技術評価)                                           │  │  │
│  │  │ ├── DeploymentName: (コード特化モデル)                          │  │  │
│  │  │ ├── Weight: 1.2（20%重視）                                      │  │  │
│  │  │ ├── Specialty: "technical"                                      │  │  │
│  │  │ └── 役割: コード品質・アーキテクチャ・技術選定を重点評価          │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  │                              │                                        │  │
│  │                              ▼                                        │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │ MetaJudge (最終統合評価)                                        │  │  │
│  │  │ ├── DeploymentName: (推論モデル)                                │  │  │
│  │  │ ├── 役割: 各Judgeの評価を分析し最終判断を統合                    │  │  │
│  │  │ └── 矛盾がある場合は解決理由も出力                               │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                       │  │
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

### 廉価版構成（開発・テスト向け、〜$2/月）

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
    "Judges": [
      {
        "JudgeId": "GeneralEvaluator",
        "DeploymentName": "gpt-5-nano"
      },
      {
        "JudgeId": "TechExpert",
        "DeploymentName": "gpt-5.1-codex-mini",
        "Specialty": "technical"
      }
    ],
    "MetaJudge": {
      "DeploymentName": "o3-mini"
    }
  },
  "WeeklySummary": {
    "DeploymentName": "o3-mini"
  }
}
```

### 本番版構成（高品質評価、〜$30/月）

```json
{
  "BatchScoring": {
    "Filtering": {
      "DeploymentName": "gpt-5-nano"
    }
  },
  "EnsembleScoring": {
    "Judges": [
      {
        "JudgeId": "GeneralEvaluator",
        "DeploymentName": "gpt-5-mini"
      },
      {
        "JudgeId": "TechExpert",
        "DeploymentName": "gpt-5-codex",
        "Specialty": "technical"
      }
    ],
    "MetaJudge": {
      "DeploymentName": "o3"
    }
  },
  "WeeklySummary": {
    "DeploymentName": "o3"
  }
}
```

※ 省略した項目はコード内のデフォルト値が使用される
※ Filteringは本番でもnanoで十分（0-10判定のため）

---

## 設定項目リファレンス

**必須** = 設定ファイルで指定が必要、**任意** = 省略可能（デフォルト値あり）

### Scoring

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `Preset` | 任意 | QualityFocused | スコアリングプリセット |
| `EnsembleRelevanceThreshold` | 任意 | 6 | アンサンブル評価での関連性閾値（0-20点） |

### BatchScoring

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `FilteringPreset` | 任意 | Normal | フィルタリング閾値プリセット（Loose/Normal/Strict） |
| `Filtering.DeploymentName` | **必須** | - | フィルタリング用モデル |

### EnsembleScoring.Judges

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `JudgeId` | **必須** | - | 識別子（GeneralEvaluator, TechExpert等） |
| `DeploymentName` | **必須** | - | このJudgeが使うモデル |
| `Specialty` | 任意 | null（汎用） | `technical`で技術評価特化 |
| `Weight` | 任意 | 1.0 | 最終スコアへの重み |

### EnsembleScoring.MetaJudge

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `DeploymentName` | **必須** | - | Meta-Judgeが使うモデル（推論モデル推奨） |

### WeeklySummary

| 項目 | 必須 | デフォルト | 説明 |
|------|:----:|:----------:|------|
| `DeploymentName` | 任意 | o3-mini | 週次まとめ生成に使うモデル（推論モデル推奨） |

---

## モデル選定ガイド

### 用途別推奨

| 用途 | 廉価版 | 本番版 | 理由 |
|------|--------|--------|------|
| Filtering | nano | nano | 高速・低コスト。0-10判定には十分 |
| GeneralEvaluator | nano | mini | 汎用的な評価能力 |
| TechExpert | nano | mini | 技術評価もChat対応モデルが必要 |
| MetaJudge | o3-mini | o3 | 推論特化。複数意見の統合に適している |
| WeeklySummary | o3-mini | o3 | 推論特化。長文の統合・要約に適している |

**注意**: すべてのJudgeは**Chat Completion対応モデル**が必要です（旧Codexモデルは非対応）

### コスト試算（100記事/日、30日運用）

| 構成 | Filtering | Ensemble | 月額目安 |
|------|---------|-------------|---------|
| 廉価版 | nano | nano×2 + o3-mini | 〜$2 |
| 本番版 | nano | mini×2 + o3 | 〜$30 |

※ nanoは$0.05/1M入力、miniは$0.25/1M入力（2025年1月時点の参考価格）

### パラメータ互換性

| モデル分類 | Temperature | MaxTokens | 備考 |
|-----------|:-----------:|:---------:|------|
| 推論モデル（o3, gpt-5系） | ✗ | ✗ | SDK側で自動処理。設定しても無視 |
| 通常モデル（gpt-4o系） | ○ | ○ | 明示的に設定可能 |

※ `ModelCapabilities.cs` でモデル名から自動判定されるため、設定ファイルでの指定は不要

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

### Judge評価（Ensemble）

```
以下の技術記事を評価し、日本語で要約。

評価項目（各0-20点）:
- relevance: キーワードとの関連性（EnsembleRelevanceScore）
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

### MetaJudge統合

```
各評価者の判断を統合し、最終スコアを決定。

入力: 各Judgeのスコアと理由
処理:
1. 各評価者のスコアと理由を分析
2. 矛盾点を特定し、妥当な解決策を提示
3. 各評価者の専門性と重みを考慮した最終スコアを決定
4. 信頼度（0.0-1.0）を算出
5. 要約を統合・改善

出力:
{
  "final_relevance": 15,
  "final_technical": 17,
  "final_novelty": 15,
  "final_impact": 16,
  "final_quality": 16,
  "final_total": 79,
  "confidence": 0.85,
  "rationale": "...",
  "consolidated_summary": "..."
}
```

---

## Articleエンティティのスコアフィールド

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `RelevanceScore` | double? | Filtering時の関連性スコア（0-10） |
| `EnsembleRelevanceScore` | int? | アンサンブル評価での関連性スコア（0-20） |
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
| アンサンブル設定 | `Infrastructure/Scoring/EnsembleScoringOptions.cs` |
| 週次まとめ設定 | `Infrastructure/Scoring/WeeklySummaryOptions.cs` |
| 週次まとめサービス | `Infrastructure/Services/WeeklySummaryService.cs` |
| 記事エンティティ | `Core/Entities/Article.cs` |
