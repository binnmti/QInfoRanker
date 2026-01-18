# 実装プラン: {プラン名}

## 概要

{プランの目的と背景を1-2段落で記述}

## ブランチ

```
{branch-name}
```

## 実行順序

```
task-01 → task-02 → task-03 → ...
```

{依存関係の説明: 並列実行可能なタスク、順次実行が必要なタスクなど}

## タスク一覧

| # | ファイル | 内容 | 依存 | ステータス |
|---|---------|------|------|-----------|
| 01 | task-01-xxx.md | {概要} | なし | pending |
| 02 | task-02-yyy.md | {概要} | 01 | pending |
| 03 | task-03-zzz.md | {概要} | 01,02 | pending |

## コミット規約

各タスク完了時:
```
<type>: <description>

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>
```

type: feat / fix / refactor / test / docs / chore

## 進捗

- [ ] ブランチ作成
- [ ] task-01 完了
- [ ] task-02 完了
- [ ] task-03 完了
- [ ] 全テスト通過 (`dotnet test`)
- [ ] コードレビュー (`/local-review`)
- [ ] mainへマージ

## 使い方

```bash
# プラン状況確認
/plan-status doc/plans/{フォルダ名}

# タスク順次実行
/plan-run doc/plans/{フォルダ名}
```

## 関連情報

- Issue: #{issue番号}（あれば）
- 関連PR: #{PR番号}（あれば）
- 参考: {関連ドキュメントへのリンク}
