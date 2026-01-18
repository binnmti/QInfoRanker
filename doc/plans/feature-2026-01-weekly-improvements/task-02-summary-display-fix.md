# タスク02: 要約が表示されない問題の修正

## 目的

「今週のおすすめ」画面で要約（サマリー）が表示されない問題を修正し、エラー時のユーザー通知を改善する。

## 背景

要約機能は実装されているが、実際には表示されていない。以下の原因が考えられる：

### 調査結果

**要約が表示されない可能性のある原因：**

1. **記事不足**: IsRelevant=true かつ LlmScore設定済みの記事が3件未満
2. **スコアリング未完了**: 記事はあるが LlmScore が null
3. **Azure OpenAI 設定問題**: Endpoint/ApiKey/DeploymentName の設定不備
4. **例外が隠れている**: CollectionService.cs でエラーがログのみになっている

**関連コード：**
- 要約生成: `WeeklySummaryService.GenerateSummaryAsync()`
- 自動生成: `CollectionService.GenerateSummaryIfNeededAsync()`
- 表示: `WeeklySummary.razor` の要約セクション（124-192行）

## 対象ファイル

- `src/QInfoRanker.Infrastructure/Services/CollectionService.cs` - エラー通知の改善
- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - 診断情報の表示
- `appsettings.json` - 設定の確認

## 実装内容

- [ ] 要約生成の前提条件（記事数、IsRelevant、LlmScore）の診断情報を表示
- [ ] 要約生成失敗時のエラーメッセージをUIに表示（現状はログのみ）
- [ ] Azure OpenAI 設定（WeeklySummary セクション）の確認と修正
- [ ] 手動生成ボタンのエラーハンドリング改善

## テスト

- [ ] 条件を満たさない場合に適切なメッセージが表示されることを確認
- [ ] 条件を満たす場合に要約が生成・表示されることを確認
- [ ] Azure OpenAI エラー時に適切なメッセージが表示されることを確認
- [ ] 既存のビルドが通ることを確認

## 完了条件

- 要約が正常に生成・表示される
- 生成できない場合は理由がユーザーに分かるように表示される
- 既存テストが通る

## 参考情報

- `src/QInfoRanker.Infrastructure/Services/WeeklySummaryService.cs` - 要約生成ロジック
- `src/QInfoRanker.Core/Entities/WeeklySummary.cs` - エンティティ定義
- `appsettings.json` の `WeeklySummary` セクション

---

## 実行結果

- **実行日時**: 2026-01-18
- **ステータス**: completed
- **変更ファイル**:
  - src/QInfoRanker.Core/Interfaces/Services/IWeeklySummaryService.cs
  - src/QInfoRanker.Infrastructure/Services/WeeklySummaryService.cs
  - src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor
  - src/QInfoRanker.Infrastructure/Services/CollectionService.cs
- **コミット**: bb43d19
- **学んだこと**:
  - Razorで日本語テキストと@expressionを連結する場合、@(expression)形式で明示的に区切る
  - SummaryDiagnosticsレコードで診断情報をまとめて管理
- **問題点**: なし
