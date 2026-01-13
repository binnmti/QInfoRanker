---
description: コミット前ローカルレビュー（CodeRabbit + Copilot + Claude）
---

# コミット前コードレビュー

複数のLLM（CodeRabbit、GitHub Copilot、Claude）を使ってコードレビューを実行し、指摘事項を修正します。

## 手順

### Step 1: 変更内容の確認
まず `git diff` と `git diff --staged` で変更内容を把握してください。

### Step 2: CodeRabbitレビュー
以下のコマンドでCodeRabbitのレビューを実行してください：

```bash
wsl bash -c "~/.local/bin/cr review --plain -t uncommitted --cwd /mnt/e/Code/Private/QInfoRanker"
```

結果を「CodeRabbitの指摘」として記録してください。

**注意**: レート制限エラー（「20分待ってください」等）が発生した場合は、「CodeRabbitの指摘: スキップ（レート制限）」と記録して、Step 3に進んでください。

### Step 3: GitHub Copilotレビュー
以下のコマンドでGitHub Copilot（gpt-5.1-codex-max）のレビューを実行してください：

```bash
copilot --model gpt-5.1-codex-max -p "以下のgit diffをコードレビューしてください。バグ、セキュリティリスク、設計上の問題点を「重大」「警告」「軽微」に分類して指摘してください: $(git diff -- . ':(exclude)*.Designer.cs')"
```

結果を「Copilotの指摘」として記録してください。

### Step 4: Claude Codeセルフレビュー
変更されたファイルを読み込み、以下の観点でレビューしてください：

- バグ・論理エラーの可能性
- セキュリティリスク（インジェクション、認証漏れ等）
- CLAUDE.mdの開発原則との整合性（SOLID、DRY、YAGNI）
- 命名・可読性
- テストの有無

結果を「Claude Codeの指摘」として記録してください。

### Step 5: 指摘事項の集約
3つのレビュー結果を以下の形式で集約してください：

```
## レビュー結果サマリー

### 重大（必ず修正）
- [CodeRabbit] xxx
- [Copilot] xxx
- [Claude] xxx

### 推奨（修正推奨）
- [CodeRabbit] xxx
- [Copilot] xxx
- [Claude] xxx

### 軽微（任意）
- [CodeRabbit] xxx
- [Copilot] xxx
- [Claude] xxx
```

### Step 6: 修正の実施
「重大」「推奨」の指摘について、ユーザーに確認の上で修正を実施してください。

### Step 7: 再レビュー（ループ）
修正後、Step 2-5を再実行してください。
以下の条件を満たしたら終了：
- 「重大」「推奨」の指摘が0件
- または3回ループした

## 出力形式

最終的に以下を報告してください：

```
## レビュー完了

- 実施回数: N回
- 修正件数: N件
- 残存指摘: N件（軽微のみ）

### 修正内容
1. xxx
2. xxx

### 残存指摘（軽微）
- xxx
```
