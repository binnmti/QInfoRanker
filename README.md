# QInfoRanker

キーワードベースの情報収集・ランキングシステム。複数のニュースソースやテック系メディアから記事を収集し、AIを活用して関連性と品質でスコアリング・ランキングします。

## 機能

- **マルチソース収集**: Hacker News、ArXiv、Reddit、Qiita、Zenn、はてな、Note、Google News、PubMed、BBC、Yahoo!ニュース、Semantic Scholarなど11以上のソースに対応
- **AIスコアリング**: Azure OpenAI を使用した2段階評価
  - 関連性フィルタリング（キーワードとの関連度）
  - アンサンブル評価（複数Judgeによる多角的評価）
- **ハイブリッドスコア**: ネイティブスコア（各ソースの人気度）とLLMスコアを組み合わせた最終スコア
- **キーワード管理**: AIによるソース推薦と英語エイリアス自動生成
- **リアルタイム収集**: バックグラウンドでの記事収集と進捗表示
- **週次まとめ**: キーワードごとに今週の記事をAIがニュース記事風に自動要約

## 技術スタック

- **フレームワーク**: ASP.NET Core 8.0 + Blazor (Server/WebAssembly)
- **言語**: C# (.NET 8.0)
- **データベース**: SQLite（デフォルト）/ SQL Server
- **AI**: Azure OpenAI
- **スクレイピング**: AngleSharp
- **ORM**: Entity Framework Core 8.0

## 必要環境

- .NET 8.0 SDK
- Azure OpenAI のエンドポイントとAPIキー

## セットアップ

1. リポジトリをクローン
   ```bash
   git clone https://github.com/your-username/QInfoRanker.git
   cd QInfoRanker
   ```

2. Azure OpenAI の設定
   `src/QInfoRanker.Web/appsettings.json` を編集:
   ```json
   "AzureOpenAI": {
     "Endpoint": "YOUR_AZURE_OPENAI_ENDPOINT",
     "ApiKey": "YOUR_API_KEY"
   }
   ```
   ※ 使用するモデルは `BatchScoring`、`EnsembleScoring`、`WeeklySummary` セクションで設定

3. アプリケーションを実行
   ```bash
   dotnet run --project src/QInfoRanker.Web
   ```

4. ブラウザで `https://localhost:5001` にアクセス

## 使い方

### キーワード管理 (`/`)
- 新しいキーワードを追加すると、AIが適切なソースを推薦
- 英語エイリアスが自動生成され、英語ソースでも検索可能

### ランキング (`/ranking`)
- キーワード、言語、ソース、期間でフィルタリング
- 各記事のスコア内訳（技術性、新規性、影響度、品質、関連性）を表示
- 日本語要約を確認可能

### 週次まとめ (`/weekly-summary`)
- キーワードごとの今週のニュースまとめを表示
- 記事収集完了時にAIが自動生成（週1回）
- 過去のまとめも閲覧可能

### ソース管理 (`/sources`)
- ソースの有効化/無効化
- API/RSS/スクレイピングのタイプ別表示

## プロジェクト構成

```
src/
├── QInfoRanker.Core/           # ドメインエンティティ・インターフェース
├── QInfoRanker.Infrastructure/ # データアクセス・コレクター・サービス
├── QInfoRanker.Web/            # Blazor Server アプリケーション
└── QInfoRanker.Web.Client/     # Blazor WebAssembly クライアント

tests/
└── QInfoRanker.Tests/          # 統合テスト
```

## スコアリングの仕組み

記事は2段階のAI評価を経て最終スコアが決まります。

```
収集記事 → [Filtering: 関連性] → 関連記事のみ → [Ensemble: 多角評価] → 最終スコア計算
```

### Filtering（関連性フィルタリング）
キーワードとの関連度を0〜10で評価し、閾値以上の記事だけを次のステージへ。

### Ensemble（アンサンブル評価）
複数のJudge（評価モデル）+ MetaJudge（統合モデル）で5軸評価（各0〜20点、合計100点満点）:
- Relevance: 最終関連性
- Technical: 技術的深さ
- Novelty: 新規性
- Impact: 実用性
- Quality: 情報の質

### 最終スコア計算
```
FinalScore = (NativeScore × Weight + LlmScore × Weight) × AuthorityBonus
```

## スコアリング設定

`appsettings.json` でプリセットを選択するだけで設定完了。

### Scoring.Preset（スコアリング方式）

| プリセット | 説明 | 内部の重み |
|-----------|------|-----------|
| **QualityFocused**（デフォルト） | 質重視: 人気がなくても良質な記事が上位に | Native 0.3 / LLM 0.7 |
| Balanced | バランス型: 人気度とAI評価を均等に | Native 0.5 / LLM 0.5 |
| PopularityFocused | 人気重視: バズった記事が上位に | Native 0.7 / LLM 0.3 |

### BatchScoring.FilteringPreset（フィルタリング強度）

| プリセット | 説明 | 内部の閾値 |
|-----------|------|-----------|
| Loose | 緩め: 幅広く記事を表示 | 2.0 |
| **Normal**（デフォルト） | 通常: 明らかに無関係な記事だけ除外 | 3.0 |
| Strict | 厳格: 関連性の高い記事のみ表示 | 6.0 |

### 設定例

```json
{
  "Scoring": {
    "Preset": "QualityFocused"
  },
  "BatchScoring": {
    "FilteringPreset": "Normal"
  }
}
```

## ライセンス

Apache License 2.0
