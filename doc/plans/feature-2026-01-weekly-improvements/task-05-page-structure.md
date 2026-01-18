# タスク05: ページ名変更とキーワード別URL化

## 目的

「トップ10」ページを「今週のおすすめ」に変更し、キーワードごとに個別のURLを持つ構造に変更する。

## 背景

現状：
- ページ名が「トップ10」
- コンボボックスでキーワードを切り替え
- URL は単一（`/top10` または `/weekly-summary`）

ユーザーの要望：
- 「今週のおすすめ」という名前に変更
- キーワードごとにユニークなページ/URL
- 例: `/weekly/量子コンピューター`, `/weekly/IT`
- 動的ルーティングでOK

## 対象ファイル

- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - ルーティング変更
- `src/QInfoRanker.Web/Components/Layout/NavMenu.razor` - ナビゲーション変更
- 関連するリンク箇所

## 実装内容

- [ ] ページのルートを `/weekly/{keyword?}` に変更（キーワードはオプショナル）
- [ ] キーワード未指定時はキーワード一覧または最初のキーワードを表示
- [ ] ナビゲーションメニューの表示名を「今週のおすすめ」に変更
- [ ] キーワード選択時にURLが変わるように（コンボボックス廃止 or URL連動）
- [ ] キーワード一覧からのリンクを各キーワードのURLに変更

## テスト

- [ ] `/weekly/量子コンピューター` でアクセスできることを確認
- [ ] `/weekly/IT` でアクセスできることを確認
- [ ] `/weekly` でデフォルト表示されることを確認
- [ ] ナビゲーションが正しく動作することを確認
- [ ] 既存のビルドが通ることを確認

## 完了条件

- キーワードごとにユニークなURLでアクセスできる
- ナビゲーションが「今週のおすすめ」表示
- 旧URL（`/top10`）からのリダイレクトまたは廃止
- 既存テストが通る

## 参考情報

- Blazor のルーティング: `@page "/weekly/{Keyword?}"`
- `src/QInfoRanker.Web/Components/Layout/NavMenu.razor` - ナビゲーション

---

## 実行結果

- **実行日時**: 2026-01-18
- **ステータス**: completed
- **変更ファイル**:
  - src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor
  - src/QInfoRanker.Web/Components/Layout/NavMenu.razor
  - src/QInfoRanker.Web/Components/Pages/Keywords.razor
- **コミット**: f46ed48
- **学んだこと**:
  - Blazorのルートパラメータは{Param?}でオプショナルに
  - OnParametersSetAsyncでURLパラメータ変更時にページ状態を更新
  - Uri.EscapeDataString/UnescapeDataStringで日本語のURLエンコード対応
- **問題点**: なし
