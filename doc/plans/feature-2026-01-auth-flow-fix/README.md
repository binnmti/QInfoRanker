# 実装プラン: 認証フロー修正

## 概要

デプロイ環境でログインボタンをクリックすると404エラーが発生する問題を修正します。
存在しない `/login` ページへのリンクを削除し、Azure AD認証に一本化することで、認証フローを整理・簡素化します。

## ブランチ

```
feature/2026-01-auth-flow-fix
```

## 実行順序

```
task-01 → task-02 → task-03
```

タスク1で認証フローを修正し、タスク2で不要コードを削除、タスク3でドキュメントを更新します。
順番に依存関係があるため、順次実行してください。

## タスク一覧

| # | ファイル | 内容 | 依存 | ステータス |
|---|---------|------|------|-----------|
| 01 | task-01-fix-login-redirect.md | `/login`リンク削除、Azure AD認証に統一 | なし | pending |
| 02 | task-02-cleanup-dev-auth.md | 開発用認証コードの整理 | 01 | pending |
| 03 | task-03-update-auth-docs.md | 認証関連ドキュメント更新 | 02 | pending |

## コミット規約

各タスク完了時:
```
<type>: <description>

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
```

## 進捗

- [x] ブランチ作成
- [x] task-01 完了
- [x] task-02 完了
- [x] task-03 完了
- [ ] 全テスト通過
- [ ] mainへマージ
