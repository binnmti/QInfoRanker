# タスク04: 記事のインクリメンタルローディング

## 目的

各カテゴリ（ニュース・技術記事・研究）で「もっと見る」ボタンによる追加読み込み機能を実装する。

## 背景

現状：
- 各カテゴリ10記事ずつ表示
- それ以上の記事は見られない

ユーザーの要望：
- 10記事の下に「もっと見る」ボタン
- ボタンを押すと次の10件（11-20）が追加
- さらにボタンを押すと次の10件（21-30）が追加...

**レイアウト要件：**
- 画面全体は一画面内に収める
- 各カテゴリごとに個別のスクロールバー
- カテゴリ枠内でスクロール（全体が縦に伸びない）

## 対象ファイル

- `src/QInfoRanker.Infrastructure/Services/ArticleService.cs` - ページング対応
- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - UI実装

## 実装内容

- [ ] ArticleService に skip/take パラメータを追加（または専用メソッド）
- [ ] 各カテゴリの表示エリアに固定高さ + overflow-y: auto を設定
- [ ] 「もっと見る」ボタンの追加（各カテゴリ下部）
- [ ] ボタン押下時に次の10件を既存リストに追加
- [ ] 全件読み込み済みの場合はボタンを非表示/無効化

## テスト

- [ ] 初期表示で10件表示されることを確認
- [ ] 「もっと見る」ボタンで追加10件が表示されることを確認
- [ ] スクロールバーがカテゴリ枠内に表示されることを確認
- [ ] 全件表示後にボタンが消えることを確認
- [ ] 既存のビルドが通ることを確認

## 完了条件

- 各カテゴリで「もっと見る」による追加読み込みができる
- 各カテゴリが固定高さでスクロール可能
- 画面全体のレイアウトが崩れない
- 既存テストが通る

## 参考情報

- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - 現在のレイアウト
- Bootstrap/CSS のスクロール設定

---

## 実行結果

- **実行日時**: 2026-01-18
- **ステータス**: completed
- **変更ファイル**:
  - src/QInfoRanker.Core/Interfaces/Services/IArticleService.cs
  - src/QInfoRanker.Infrastructure/Services/ArticleService.cs
  - src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor
- **コミット**: 7774dc2
- **学んだこと**:
  - クエリ構築ロジックを共通メソッドに抽出することでDRY原則を維持
  - BlazorのList<T>でAddRangeを使いインクリメンタルに記事追加
- **問題点**: なし
