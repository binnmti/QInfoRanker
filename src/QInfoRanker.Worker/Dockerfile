# QInfoRanker Worker - 定期収集ジョブ用Dockerfile
# マルチステージビルドで軽量イメージを作成

# ビルドステージ
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# プロジェクトファイルをコピーして依存関係を復元
COPY ["src/QInfoRanker.Worker/QInfoRanker.Worker.csproj", "src/QInfoRanker.Worker/"]
COPY ["src/QInfoRanker.Infrastructure/QInfoRanker.Infrastructure.csproj", "src/QInfoRanker.Infrastructure/"]
COPY ["src/QInfoRanker.Core/QInfoRanker.Core.csproj", "src/QInfoRanker.Core/"]
RUN dotnet restore "src/QInfoRanker.Worker/QInfoRanker.Worker.csproj"

# ソースコードをコピーしてビルド（必要なディレクトリのみ）
COPY ["src/QInfoRanker.Core/", "src/QInfoRanker.Core/"]
COPY ["src/QInfoRanker.Infrastructure/", "src/QInfoRanker.Infrastructure/"]
COPY ["src/QInfoRanker.Worker/", "src/QInfoRanker.Worker/"]

WORKDIR "/src/src/QInfoRanker.Worker"
RUN dotnet build "QInfoRanker.Worker.csproj" -c Release -o /app/build

# パブリッシュステージ
FROM build AS publish
RUN dotnet publish "QInfoRanker.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 実行ステージ（軽量ランタイムイメージ）
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# タイムゾーン設定（ログ出力用）
ENV TZ=Asia/Tokyo

# ヘルスチェック用の環境変数
ENV ASPNETCORE_ENVIRONMENT=Production

# パブリッシュ結果をコピー
COPY --from=publish /app/publish .

# エントリーポイント
ENTRYPOINT ["dotnet", "QInfoRanker.Worker.dll"]
