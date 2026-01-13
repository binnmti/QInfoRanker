# Worker デプロイ手順

このドキュメントでは、定期収集Worker（Container Apps Job）のデプロイ手順を説明します。

## 前提条件

- Azure CLIがインストールされていること
- Azureにログイン済みであること（`az login`）
- インフラが構築済みであること（`terraform apply`完了）
- **Docker不要**: ACR Tasks を使用してクラウド上でビルドします

## 1. ACR Tasks でビルド＆プッシュ（推奨）

**Docker Desktop不要**で、Azure上でイメージをビルドできます。

### Bash（Mac/Linux/Git Bash）

```bash
# プロジェクトルートに移動
cd /path/to/QInfoRanker

# ACR Tasks でビルド＆プッシュ（一発コマンド）
az acr build \
  --registry qinforankeracr \
  --image qinforanker-worker:latest \
  --file src/QInfoRanker.Worker/Dockerfile \
  .
```

### PowerShell（Windows）

```powershell
# プロジェクトルートに移動
cd C:\path\to\QInfoRanker

# ACR Tasks でビルド＆プッシュ
az acr build --registry qinforankeracr --image qinforanker-worker:latest --file src/QInfoRanker.Worker/Dockerfile .
```

ビルドが成功すると、自動的にACRにプッシュされます。

## 2. （代替）ローカルDockerでビルドする場合

Docker Desktopがインストールされている場合は、ローカルでビルドすることもできます。

```bash
# ACRにログイン
ACR_NAME="qinforankeracr"
ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --query loginServer -o tsv)
az acr login --name $ACR_NAME

# Dockerイメージをビルド
docker build -t qinforanker-worker:latest -f src/QInfoRanker.Worker/Dockerfile .

# タグ付け＆プッシュ
docker tag qinforanker-worker:latest $ACR_LOGIN_SERVER/qinforanker-worker:latest
docker push $ACR_LOGIN_SERVER/qinforanker-worker:latest
```

## 3. Container Apps Jobの更新

イメージをプッシュした後、Container Apps Jobが自動的に新しいイメージを使用します。
手動でジョブを実行してテストする場合：

```bash
# ジョブを手動実行
az containerapp job start \
  --name qinforanker-job \
  --resource-group qinforanker-rg

# 実行履歴を確認
az containerapp job execution list \
  --name qinforanker-job \
  --resource-group qinforanker-rg \
  --output table
```

## 4. ログの確認

```bash
# 最新の実行ログを確認
az containerapp job logs show \
  --name qinforanker-job \
  --resource-group qinforanker-rg \
  --follow
```

または、Azure Portalの Log Analytics ワークスペースで確認：
1. Azure Portal → `qinforanker-log` を開く
2. ログ → 以下のクエリを実行：

```kusto
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "qinforanker-job"
| order by TimeGenerated desc
| take 100
```

## クイックコマンド（まとめ）

### 最短デプロイ（ACR Tasks）

```bash
# これだけでOK
az acr build --registry qinforankeracr --image qinforanker-worker:latest --file src/QInfoRanker.Worker/Dockerfile .

echo "デプロイ完了！"
echo "手動実行: az containerapp job start --name qinforanker-job --resource-group qinforanker-rg"
```

### ローカルDocker版（全手順）

```bash
ACR_NAME="qinforankeracr"
ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --query loginServer -o tsv)

az acr login --name $ACR_NAME

docker build -t qinforanker-worker:latest -f src/QInfoRanker.Worker/Dockerfile .
docker tag qinforanker-worker:latest $ACR_LOGIN_SERVER/qinforanker-worker:latest
docker push $ACR_LOGIN_SERVER/qinforanker-worker:latest

echo "デプロイ完了！"
```

## トラブルシューティング

### ACR Tasksでビルドエラーが出る場合

1. `.dockerignore`が存在するか確認（不要なファイルを除外）
2. Dockerfileのパスが正しいか確認
3. プロジェクトルートから実行しているか確認

```bash
# ビルドログを詳細表示
az acr build --registry qinforankeracr --image qinforanker-worker:latest --file src/QInfoRanker.Worker/Dockerfile . --verbose
```

### イメージのプルに失敗する場合

```bash
# ACRの認証情報を確認
az acr credential show --name qinforankeracr

# Container Apps JobのACR設定を確認
az containerapp job show \
  --name qinforanker-job \
  --resource-group qinforanker-rg \
  --query "properties.configuration.registries"
```

### DB接続エラーの場合

1. SQL Serverのファイアウォール設定を確認
2. 接続文字列が正しいか確認
3. Container Apps環境からSQL Serverへのネットワーク接続を確認

### AIサービス接続エラーの場合

1. Azure OpenAIのエンドポイントとAPIキーを確認
2. 環境変数が正しく設定されているか確認：

```bash
az containerapp job show \
  --name qinforanker-job \
  --resource-group qinforanker-rg \
  --query "properties.template.containers[0].env"
```

## スケジュールの変更

現在のスケジュール: **毎日 06:00 JST**（UTC 21:00）

スケジュールを変更する場合は、`infra/variables.tf`を編集して`terraform apply`を実行：

```hcl
variable "collection_schedule" {
  default = "0 21 * * *"  # 毎日06:00 JST
  # default = "0 21 * * 1,3,5"  # 週3回（月水金）06:00 JST
  # default = "0 21 * * 1"  # 毎週月曜06:00 JST
}
```

または、Azure CLIで直接変更：

```bash
az containerapp job update \
  --name qinforanker-job \
  --resource-group qinforanker-rg \
  --cron-expression "0 21 * * *"
```

## 関連ファイル

| ファイル | 説明 |
|---------|------|
| [src/QInfoRanker.Worker/](../src/QInfoRanker.Worker/) | Workerプロジェクト |
| [src/QInfoRanker.Worker/Dockerfile](../src/QInfoRanker.Worker/Dockerfile) | Dockerイメージ定義 |
| [.dockerignore](../.dockerignore) | Dockerビルド時の除外設定 |
| [infra/container-job.tf](../infra/container-job.tf) | Container Apps Jobのインフラ定義 |
| [infra/variables.tf](../infra/variables.tf) | スケジュール設定など |
| [doc/COLLECTION_OVERVIEW.md](./COLLECTION_OVERVIEW.md) | 収集ロジックの詳細 |
