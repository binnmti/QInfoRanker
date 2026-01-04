# CLAUDE.md

このファイルはClaude Codeがプロジェクトを効率的に理解・操作するためのガイドです。

## プロジェクト概要

QInfoRankerは、キーワードベースの情報収集・ランキングシステムです。複数のニュースソースから記事を収集し、Azure OpenAIを使用してスコアリング・ランキングします。

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
- `Scoring/AzureOpenAIScoringService.cs` - AIスコアリングロジック
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
3. `Program.cs` でDI登録
4. `Core/Enums/SourceType.cs` に新しいソースタイプを追加（必要に応じて）

### スコアリングの流れ
1. **関連性評価** (RelevanceBatch): キーワードとの関連度を0-10でスコアリング
2. **品質評価** (QualityBatch): 閾値を超えた記事に対して4軸評価
   - Technical (0-25): 技術的深さ
   - Novelty (0-25): 新規性
   - Impact (0-25): 影響度
   - Quality (0-25): 品質
3. **最終スコア**: `(NativeScore × Weight + LlmScore × Weight) × AuthorityBonus`

## 設定

### 環境変数/appsettings.json
- `AzureOpenAI:Endpoint` - Azure OpenAI エンドポイント
- `AzureOpenAI:ApiKey` - APIキー
- `AzureOpenAI:DeploymentName` - デプロイメント名（gpt-4o-mini）
- `UseSqlite` - SQLite使用フラグ（false時はSQL Server）

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

## よくある作業

### キーワード関連の変更
- キーワードサービス: `Infrastructure/Services/KeywordService.cs`
- ソース推薦: `Infrastructure/Services/SourceRecommendationService.cs`
- キーワードUI: `Web/Components/Pages/Keywords.razor`

### 収集関連の変更
- 収集サービス: `Infrastructure/Services/CollectionService.cs`
- バックグラウンド処理: `Infrastructure/Services/CollectionBackgroundService.cs`
- 収集キュー: `Infrastructure/Services/CollectionQueue.cs`

### スコアリング関連の変更
- スコアリングサービス: `Infrastructure/Scoring/AzureOpenAIScoringService.cs`
- バッチ処理設定: `appsettings.json` の `BatchScoring` セクション

## 注意事項

- Azure OpenAI APIはコストがかかるため、開発時は `BatchScoring:EnableBatchProcessing` を確認
- 収集処理は時間がかかるため、バックグラウンドキューを使用
- SignalRタイムアウトは2分に設定済み
- **タスク完了時はREADME.mdも確認・追従すること**（機能追加・設定変更などがあればドキュメントも更新）
