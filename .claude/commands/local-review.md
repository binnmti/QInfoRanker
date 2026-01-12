---
description: コミット前ローカルレビュー（CodeRabbit + Claude）
---

# コミット前コードレビュー

複数のLLMを使ってコードレビューを実行し、指摘事項を修正します。

## 手順

### Step 1: 変更内容の確認
まず `git diff` と `git diff --staged` で変更内容を把握してください。

### Step 2: CodeRabbitレビュー
以下のコマンドでCodeRabbitのレビューを実行してください：

```bash
wsl ~/.local/bin/cr review --plain -t uncommitted --cwd /mnt/e/Code/Private/QInfoRanker
```

結果を「CodeRabbitの指摘」として記録してください。

### Step 3: Claude Codeセルフレビュー
変更されたファイルを読み込み、以下の観点でレビューしてください：

- バグ・論理エラーの可能性
- セキュリティリスク（インジェクション、認証漏れ等）
- CLAUDE.mdの開発原則との整合性（SOLID、DRY、YAGNI）
- 命名・可読性
- テストの有無

結果を「Claude Codeの指摘」として記録してください。

### Step 4: 指摘事項の集約
両方のレビュー結果を以下の形式で集約してください：

```
## レビュー結果サマリー

### 重大（必ず修正）
- [CodeRabbit] xxx
- [Claude] xxx

### 推奨（修正推奨）
- [CodeRabbit] xxx
- [Claude] xxx

### 軽微（任意）
- [CodeRabbit] xxx
- [Claude] xxx
```

### Step 5: 修正の実施
「重大」「推奨」の指摘について、ユーザーに確認の上で修正を実施してください。

### Step 6: 再レビュー（ループ）
修正後、Step 2-4を再実行してください。
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
