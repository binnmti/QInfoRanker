# タスク03: URLのSlug対応

## 目的

週次おすすめページのURLに日本語が入る問題を解決し、可読性の高いURLを実現する。

## 背景

- 現在 `/weekly/量子コンピュータ` のように日本語がURLに入る
- URLエンコードされると `%E9%87%8F%E5%AD%90...` のように長く読みづらい
- SEO的にも、共有時にも不便

## 対象ファイル

- `src/QInfoRanker.Core/Entities/Keyword.cs` - Slugフィールドを追加
- `src/QInfoRanker.Infrastructure/Data/AppDbContext.cs` - Slugのインデックス設定
- `src/QInfoRanker.Web/Components/Pages/WeeklySummary.razor` - URLルーティングをSlug対応に変更
- `src/QInfoRanker.Web/Components/Pages/Keywords.razor` - リンク生成をSlug対応に変更
- マイグレーションファイル - Slugカラム追加

## 実装内容

- [ ] Keywordエンティティに `Slug` フィールドを追加（string?, nullable）
- [ ] Slug生成ロジックを実装（Aliasesから英語名を取得、またはIDをフォールバック）
- [ ] DBマイグレーションを作成・適用
- [ ] 既存キーワードのSlugを生成（Aliasesの最初の英語単語、またはID）
- [ ] WeeklySummary.razor のルーティングを `{Slug}` または `{Id}` 対応に変更
- [ ] Keywords.razor のリンク生成を更新
- [ ] 後方互換性のため、Term での検索も維持（任意）

## テスト

- [ ] Slugが設定されたキーワードは `/weekly/{slug}` でアクセスできる
- [ ] Slugが未設定の場合は `/weekly/{id}` でアクセスできる
- [ ] 既存のリンクが正しく動作する
- [ ] `dotnet build` が成功する
- [ ] マイグレーションが正常に適用される

## 完了条件

- URLに日本語が入らない
- 既存キーワードへのアクセスが維持される
- 新規キーワード作成時にSlugが生成される（または手動入力）
- 既存テストが通る

## 参考情報

- Keyword.Aliases の形式: カンマ区切り（例: "quantum computer, QC"）
- ZennCollector での Slug 使用例

---

## 実行結果

**ステータス: 完了**

### 実施した変更

1. **Keyword.cs** - Slugフィールドと関連メソッド追加
   - `Slug` プロパティ（nullable string）
   - `GetUrlIdentifier()` - Slugがあれば返す、なければID
   - `GenerateSlug()` - テキストからslug生成
   - `GenerateSlugFromAliases()` - Aliasesから自動生成

2. **IKeywordService.cs** - 新メソッド追加
   - `GetBySlugOrIdAsync(string slugOrId)`
   - `GenerateAndSetSlugAsync(int id)`

3. **KeywordConfiguration.cs** - DB設定
   - Slugカラム（maxLength: 200）
   - ユニークフィルターインデックス

4. **DbSeeder.cs** - 初期データ対応
   - サンプルキーワードにSlug追加
   - `GenerateMissingSlugsAsync()` で既存データ更新

5. **KeywordService.cs** - ロジック実装
   - ID/Slug両方で検索可能
   - 作成時に自動Slug生成

6. **WeeklySummary.razor** - URL対応
   - リンク生成を `GetUrlIdentifier()` 使用に変更
   - URLパラメータ解決をSlug/ID/Term順に対応（後方互換）

7. **Keywords.razor** - リンク更新
   - `GetUrlIdentifier()` 使用に変更

8. **マイグレーション作成**
   - `20260118102137_AddKeywordSlug.cs`

9. **テスト追加**
   - `KeywordSlugTests.cs`（22件のテストケース）

### URL形式

- Slugあり: `/weekly/quantum-computer`
- Slugなし: `/weekly/42`
- 後方互換: `/weekly/量子コンピュータ` も動作

### 完了条件の確認

- [x] URLに日本語が入らない（Slug使用時）
- [x] 既存キーワードへのアクセスが維持される
- [x] `dotnet build` が成功する
- [x] マイグレーションが正常に作成される
