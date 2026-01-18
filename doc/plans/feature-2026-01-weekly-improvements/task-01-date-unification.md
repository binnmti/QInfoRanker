# タスク01: 日付表示をPublishedAtに統一

## 目的

「今週のおすすめ」画面で、全カテゴリの日付表示を記事の元の公開日（PublishedAt）に統一する。

## 背景

現状、カテゴリによって表示される日付が異なっている：

| カテゴリ | 現状 | あるべき姿 |
|---------|------|-----------|
| ニュース | PublishedAt | PublishedAt |
| 技術記事 | CollectedAt | PublishedAt |
| 研究 | CollectedAt | PublishedAt |

ユーザーにとって「記事がいつ公開されたか」が重要な情報であり、収集日は本質的ではない。

### 調査結果

- Article エンティティには `PublishedAt`（公開日）と `CollectedAt`（収集日）の両方が存在
- 全てのコレクターで PublishedAt は適切に設定されている
- WeeklySummary.razor の表示箇所：
  - ニュース: 247-249行目（PublishedAt）
  - 技術記事: 300行目（CollectedAt）
  - 研究: 350行目（CollectedAt）

## 対象ファイル

- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - 日付表示の変更

## 実装内容

- [ ] 技術記事カラム（300行目付近）の `CollectedAt` を `PublishedAt` に変更
- [ ] 研究カラム（350行目付近）の `CollectedAt` を `PublishedAt` に変更
- [ ] PublishedAt が null の場合の表示を検討（空欄 or CollectedAt をフォールバック）

## テスト

- [ ] 技術記事の日付が PublishedAt で表示されることを確認
- [ ] 研究の日付が PublishedAt で表示されることを確認
- [ ] PublishedAt が null の記事がある場合の表示を確認
- [ ] 既存のビルドが通ることを確認

## 完了条件

- 全カテゴリで PublishedAt が表示される
- PublishedAt が null の場合も適切に処理される
- 既存テストが通る

## 参考情報

- `src/QInfoRanker.Core/Entities/Article.cs` - Article エンティティ定義
- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - 対象ファイル

---

## 実行結果

- **実行日時**: 2026-01-18
- **ステータス**: completed
- **変更ファイル**:
  - src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor
- **コミット**: cb20d24
- **学んだこと**:
  - 既存のニュースカラムの実装パターン（PublishedAt.HasValue チェック）に合わせることで一貫性を維持
  - PublishedAt が null の場合は空欄表示（不正確な情報を避ける）
- **問題点**: なし
