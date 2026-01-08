# CLAUDE.md

このファイルはClaude Codeがプロジェクトを効率的に理解・操作するためのガイドです。

## プロジェクト概要

QInfoRankerは、キーワードベースの情報収集・ランキングシステムです。複数のニュースソースから記事を収集し、Azure OpenAIを使用してスコアリング・ランキングします。

## ユビキタス言語（用語定義）

コード・設定・ドキュメント間で統一された用語。新しいコードや設定を追加する際は必ずこの用語に従うこと。

| 用語 | 設定キー | 説明 |
|------|----------|------|
| **Filtering** | `BatchScoring.Filtering` | 関連性フィルタリング。0-10点で判定し閾値未満を除外 |
| **Ensemble** | `EnsembleScoring` | アンサンブル評価。複数Judge + MetaJudgeによる本評価（常に有効） |
| **Judge** | `EnsembleScoring.Judges` | 個別の評価モデル（GeneralEvaluator, TechExpert等） |
| **MetaJudge** | `EnsembleScoring.MetaJudge` | 各Judgeの評価を統合する最終評価モデル |
| **EnsembleRelevanceScore** | `Article.EnsembleRelevanceScore` | アンサンブル評価での関連性スコア（0-20点） |
| **EnsembleRelevanceThreshold** | `Scoring.EnsembleRelevanceThreshold` | アンサンブル評価での関連性閾値（この値未満は除外） |

### 廃止された用語（使用禁止）

| 旧用語 | 新用語 | 理由 |
|--------|--------|------|
| Stage1 | Filtering | 役割が明確になるため |
| Stage2 | Ensemble | アンサンブル評価を表す |
| Stage2RelevanceScore | EnsembleRelevanceScore | アンサンブル評価であることを明示 |
| Stage2RelevanceThreshold | EnsembleRelevanceThreshold | 同上 |

## アーキテクチャ

Clean Architectureに基づいた3層構造:

```
src/
├── QInfoRanker.Core/           # ドメイン層（エンティティ・インターフェース）
├── QInfoRanker.Infrastructure/ # インフラ層（データアクセス・外部サービス）
├── QInfoRanker.Web/            # プレゼンテーション層（Blazor Server）
└── QInfoRanker.Web.Client/     # Blazor WebAssembly クライアント
```

## 主要ファイル

### Core層
- `Entities/Article.cs` - 記事エンティティ（スコアリングフィールド含む）
- `Entities/Keyword.cs` - キーワードエンティティ（エイリアス、関連ソース）
- `Entities/Source.cs` - データソースエンティティ
- `Interfaces/` - サービス・コレクターのインターフェース定義

### Infrastructure層
- `Collectors/` - 各ソース用コレクター（HackerNews, ArXiv, Qiita等）
- `Data/AppDbContext.cs` - Entity Framework Core DbContext
- `Scoring/ScoringService.cs` - AIスコアリングロジック
- `Services/` - ビジネスロジックサービス実装

### Web層
- `Program.cs` - アプリケーションエントリーポイント・DI設定
- `Components/Pages/` - Razorコンポーネント（Home, Ranking, Keywords, Sources）
- `appsettings.json` - 設定ファイル（Azure OpenAI、スコアリング設定）

## 開発原則

- **TDD**: Red→Green→Refactorの順で実装。新機能は必ずテストから書く
- **SOLID**: 特にSRP（単一責任）とDIP（依存性逆転）を意識
- **YAGNI**: 必要になるまで作らない
- **DRY**: 3回出たら抽象化を検討

### コード規約
- マジックナンバー禁止（定数または設定値として定義）
- コメントより分かりやすい命名を優先
- 外部ライブラリは既存で使っているものを優先
- 迷ったらMicrosoftのガイドラインに従う

### やらないこと
- God Class（肥大化したクラス）
- 既存の設計パターンを無視した独自実装
- テストなしのPR

## 開発ガイドライン

### ビルド・実行
```bash
# 実行
dotnet run --project src/QInfoRanker.Web

# ビルド
dotnet build

# テスト
dotnet test
```

### データベース
- デフォルトはSQLite (`QInfoRanker.db`)
- マイグレーションは `Infrastructure/Data/Migrations/` にあり
- `DbSeeder.SeedAsync()` で初期データ投入

### 新しいコレクター追加時
1. `Core/Interfaces/IArticleCollector.cs` を実装
2. `Infrastructure/Collectors/` に配置
3. `Infrastructure/DependencyInjection.cs` でDI登録
4. `Core/Enums/SourceType.cs` に新しいソースタイプを追加（必要に応じて）

### スコアリングの流れ
1. **Filtering（関連性フィルタリング）**: キーワードとの関連度を0-10でスコアリング、閾値未満を除外
2. **Ensemble（アンサンブル評価）**: 複数Judge + MetaJudgeで5軸評価（各0-20点、合計100点満点）
   - relevance: 最終関連性（EnsembleRelevanceScore）
   - technical: 技術的深さ
   - novelty: 新規性
   - impact: 実用性
   - quality: 情報の質
3. **最終スコア**: `(NativeScore × Weight + LlmScore × Weight) × AuthorityBonus`

詳細は [SCORING_OVERVIEW.md](SCORING_OVERVIEW.md) を参照。

## 設定

### 環境変数/appsettings.json
- `AzureOpenAI:Endpoint` - Azure OpenAI エンドポイント
- `AzureOpenAI:ApiKey` - APIキー
- `UseSqlite` - SQLite使用フラグ（false時はSQL Server）
- モデル設定は `BatchScoring.Filtering` / `EnsembleScoring` / `WeeklySummary` セクションで指定

### スコアリングプリセット
- `Scoring:Preset` - スコアリング方式
  - `QualityFocused`（デフォルト）: 質重視（Native 0.3 / LLM 0.7）
  - `Balanced`: バランス型（Native 0.5 / LLM 0.5）
  - `PopularityFocused`: 人気重視（Native 0.7 / LLM 0.3）
- `BatchScoring:FilteringPreset` - フィルタリング強度
  - `Loose`: 緩め（閾値 2.0）
  - `Normal`（デフォルト）: 通常（閾値 3.0）
  - `Strict`: 厳格（閾値 6.0）

### 関連ファイル
- プリセット定義: `Infrastructure/Scoring/ScoringPreset.cs`
- スコアリングオプション: `Infrastructure/Scoring/ScoringOptions.cs`
- バッチオプション: `Infrastructure/Scoring/BatchScoringOptions.cs`
- アンサンブルオプション: `Infrastructure/Scoring/EnsembleScoringOptions.cs`
- 週次まとめオプション: `Infrastructure/Scoring/WeeklySummaryOptions.cs`
- スコアリングサービス: `Infrastructure/Scoring/ScoringService.cs`

## よくある作業

### キーワード関連の変更
- キーワードサービス: `Infrastructure/Services/KeywordService.cs`
- ソース推薦: `Infrastructure/Services/SourceRecommendationService.cs`
- キーワードUI: `Web/Components/Pages/Keywords.razor`

### 収集関連の変更
- 収集サービス: `Infrastructure/Services/CollectionService.cs`
- バックグラウンド処理: `Infrastructure/Services/CollectionBackgroundService.cs`
- 収集キュー: `Infrastructure/Services/CollectionQueue.cs`
- 進捗通知: `Infrastructure/Services/CollectionProgressNotifier.cs`
- イベント定義: `Core/Events/CollectionEvents.cs`

### 進捗通知の仕組み
収集処理は `IProgress<ScoringProgress>` コールバックを使用してリアルタイム進捗を報告:
- **Filtering**: 関連性フィルタリングの進捗と通過件数を表示
- **Ensemble**: アンサンブル評価の進捗を表示
- UIは5秒ごとのポーリングで `CollectionQueue.GetAllStatuses()` から更新

### スコアリング関連の変更
- スコアリングサービス: `Infrastructure/Scoring/ScoringService.cs`
- 設定セクション: `appsettings.json` の `Scoring`, `BatchScoring`, `EnsembleScoring`

## 注意事項

- Azure OpenAI APIはコストがかかるため、開発時は廉価モデル（nano系）を使用
- 収集処理は時間がかかるため、バックグラウンドキューを使用
- SignalRタイムアウトは2分に設定済み
- **タスク完了時はREADME.mdも確認・追従すること**（機能追加・設定変更などがあればドキュメントも更新）
