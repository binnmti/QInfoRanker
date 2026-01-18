# タスク02: プラスボタンの表示条件修正

## 目的

週次おすすめの概要セクションにあるプラスボタン（デバッグ用）を、認証済みユーザーのみに表示するように制限する。

## 背景

- プラスボタンはデバッグ用として追加された機能
- 現在は未認証ユーザーにも表示されている
- デバッグ機能は認証済みユーザー（開発者・管理者）のみが使用すべき

## 対象ファイル

- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - プラスボタンの表示条件を追加

## 実装内容

- [ ] プラスボタンの現在の実装を確認
- [ ] `<AuthorizeView>` でプラスボタンを囲む
- [ ] 認証済みユーザーのみ表示されるように修正
- [ ] 必要に応じて開発環境のみ表示するオプションも検討

## テスト

- [ ] 未認証ユーザーにはプラスボタンが表示されない
- [ ] 認証済みユーザーにはプラスボタンが表示される
- [ ] プラスボタンの機能自体は正常に動作する
- [ ] `dotnet build` が成功する

## 完了条件

- プラスボタンが認証済みユーザーのみに表示される
- 未認証ユーザーのUIが適切
- 既存テストが通る

## 参考情報

- Blazor の AuthorizeView コンポーネント
- 他のページでの認証チェック実装

---

## 実行結果

**ステータス: 完了**

### 実施した変更

**WeeklySummary.razor** (206-219行目):
- プラスボタン（サマリー再生成ボタン）を `<AuthorizeView>` と `<Authorized>` タグで囲む

```razor
<AuthorizeView>
    <Authorized>
        <button class="btn btn-sm btn-outline-secondary" @onclick="RegenerateSummary" ...>
            ...
        </button>
    </Authorized>
</AuthorizeView>
```

### 確認結果

- `_Imports.razor` に `@using Microsoft.AspNetCore.Components.Authorization` が既存
- 追加のusingディレクティブ不要

### 完了条件の確認

- [x] プラスボタンが認証済みユーザーのみに表示される
- [x] 未認証ユーザーにはボタンが表示されない
- [x] `dotnet build` が成功する
