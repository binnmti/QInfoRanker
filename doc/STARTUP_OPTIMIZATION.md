# 起動パフォーマンス最適化計画

## 現状分析

### コールドスタートの主要原因

#### 1. Azure SQL Database Serverless の自動停止 (最大の要因)
- 現在の設定: `auto_pause_delay_in_minutes = 60`（1時間無操作で自動停止）
- 再起動にかかる時間: **30秒〜60秒以上**
- 現在の対策: `Connection Timeout=120` で接続待機時間を延長しているが、根本解決にはならない

#### 2. アプリケーション起動時のDB処理（ブロッキング）
```csharp
// Program.cs 78-84行目
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(context, seedSampleData); // ← DBが起動するまでブロック
}
```
- `Database.MigrateAsync()` がDB接続を待つ
- DBがauto-pauseしている場合、ここで長時間ブロック

#### 3. JITコンパイルによる起動オーバーヘッド
- ReadyToRunが無効のため、初回起動時にすべてのコードがJITコンパイルされる
- 影響: 起動時間が1〜3秒増加

#### 4. 現在のポジティブ要素
- ✅ App Service Plan: `always_on = true` (アプリ自体のコールドスタートは防止)
- ✅ WebSocket: 有効化済み
- ✅ Health Check: `/health` で設定済み

---

## 改善提案

### Phase 1: 即効性の高い改善（推奨）

#### 1.1 ReadyToRun (R2R) コンパイルの有効化
**効果**: 起動時間を20-50%短縮
**リスク**: 低（バイナリサイズ増加のみ）

```xml
<!-- QInfoRanker.Web.csproj に追加 -->
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

#### 1.2 Azure SQL Database の auto_pause 無効化
**効果**: DBコールドスタートを完全に排除（30-60秒削減）
**リスク**: コスト増加（ただしGeneral Purpose Serverlessは使用量課金）
**考慮事項**: min_capacity=0.5のままでも起動済み状態を維持

```hcl
# sql-database.tf の変更
auto_pause_delay_in_minutes = -1  # 自動停止を無効化
```

### Phase 2: アプリケーション層の最適化

#### 2.1 起動時のDB処理を非同期化
**効果**: 初期リクエストの応答時間を短縮
**リスク**: 中（起動直後のリクエストでDBが未準備の可能性）

```csharp
// DbSeederをHostedServiceに移行
public class DatabaseInitializationService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 起動とは非同期でDB初期化を実行
    }
}
```

#### 2.2 Application Initialization (IIS)
**効果**: ウォームアップリクエストによる事前準備
**リスク**: 低

```xml
<!-- web.config に追加 -->
<applicationInitialization doAppInitAfterRestart="true">
  <add initializationPage="/health" />
</applicationInitialization>
```

### Phase 3: インフラ層の最適化

#### 3.1 Health Check Warm-up の活用
現在の設定を活用して、App Serviceのヘルスチェックがウォームアップを兼ねる：
- `health_check_path = "/health"` (既存)
- `health_check_eviction_time_in_min = 5` (既存)

#### 3.2 スロットデプロイ（将来検討）
- ステージングスロットでウォームアップ後にスワップ
- B1プランでは利用不可（S1以上が必要）

---

## 推奨実装順序

### Step 1: 低リスク・高効果（すぐに実施）
1. ✅ ReadyToRun有効化 (`PublishReadyToRun=true`)
2. ✅ SQL Database auto_pause無効化 (`auto_pause_delay_in_minutes = -1`)
3. ✅ Application Initialization設定

### Step 2: 中リスク・中効果（テスト後に実施）
4. 起動時DB処理の非同期化

### Step 3: 要コスト検討
5. App Service Planのスケールアップ（S1以上でスロット利用可能に）
6. Azure SQL Database のスケールアップ

---

## 期待される効果

| 改善項目 | 現状 | 改善後 | 削減時間 |
|---------|------|--------|---------|
| DB cold start | 30-60秒 | 0秒 | -30〜60秒 |
| JIT compilation | 1-3秒 | 0.3-1秒 | -0.7〜2秒 |
| 起動時DB処理 | ブロック | 非同期 | 応答速度改善 |
| **合計** | **31-63秒** | **0.3-1秒** | **約30-62秒削減** |

目標の「3秒以内にページ表示」は、上記Phase 1の実装で達成可能と見込まれます。

---

## コスト影響

### Azure SQL Database Serverless (auto_pause無効化)
- 現在: 使用していない時間は課金なし
- 変更後: 最小vCore (0.5) で常時稼働
- 追加コスト: 約 $5-15/月（リージョンによる）

### ReadyToRun
- ビルド時間: 若干増加
- バイナリサイズ: 約2-3倍に増加
- 追加コスト: なし

---

## 監視指標

改善効果を測定するための指標：
1. **初回リクエスト応答時間**: Application Insights で計測
2. **起動〜Health Check成功までの時間**: App Service診断ログ
3. **DB接続確立時間**: EF Core ログ
