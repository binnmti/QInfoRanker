# 実装プラン: 今週のおすすめ画面の改善

## 概要

「今週のおすすめ」（WeeklySummary）画面の複数の改善を行う。日付表示の統一、要約機能の修正と履歴閲覧機能の追加、記事のインクリメンタルローディング、ページ構造の変更を含む。

ユーザー体験の向上と機能の一貫性を目指した改善プラン。

## ブランチ

```
feature/2026-01-weekly-improvements
```

## 実行順序

```
task-01 → task-02 → task-03 → task-04 → task-05 → task-06
```

- task-01（日付統一）は独立して実行可能
- task-02（要約表示修正）は独立して実行可能
- task-03（要約履歴）は task-02 完了後が望ましい
- task-04（インクリメンタルローディング）は独立して実行可能
- task-05（ページ構造変更）は最後に実行（URL変更を伴うため）
- task-06（未認証レイアウト）は task-05 完了後に実行

## タスク一覧

| # | ファイル | 内容 | 依存 | ステータス |
|---|---------|------|------|-----------|
| 01 | task-01-date-unification.md | 日付表示をPublishedAtに統一 | なし | completed |
| 02 | task-02-summary-display-fix.md | 要約が表示されない問題の修正 | なし | completed |
| 03 | task-03-summary-history.md | 要約の履歴閲覧機能 | 02 | completed |
| 04 | task-04-incremental-loading.md | 記事のインクリメンタルローディング | なし | completed |
| 05 | task-05-page-structure.md | ページ名変更とキーワード別URL化 | なし | completed |
| 06 | task-06-anonymous-layout.md | 未認証ユーザー向けレイアウト変更 | 05 | completed |

## コミット規約

各タスク完了時:
```
<type>: <description>

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
```

## 進捗

- [x] ブランチ作成
- [x] task-01 完了（日付統一）
- [x] task-02 完了（要約表示修正）
- [x] task-03 完了（要約履歴）
- [x] task-04 完了（インクリメンタルローディング）
- [x] task-05 完了（ページ構造変更）
- [x] task-06 完了（未認証レイアウト）
- [ ] 全テスト通過
- [ ] mainへマージ
