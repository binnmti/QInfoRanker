using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using QInfoRanker.Infrastructure;
using QInfoRanker.Web;
using QInfoRanker.Infrastructure.Data;
using QInfoRanker.Web.Components;
using QInfoRanker.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 認証設定
// ============================================================================
// 切り替え条件:
//   - appsettings.json の AzureAd:ClientId が有効な値（"YOUR_CLIENT_ID" 以外）の場合
//     → Azure AD (Entra ID) 認証を使用（本番環境向け）
//   - AzureAd:ClientId が未設定または "YOUR_CLIENT_ID" の場合
//     → DevAuthenticationHandler によるダミー認証（開発環境向け）
//
// 本番環境での設定方法:
//   1. Azure Portal で App Registration を作成
//   2. appsettings.json または環境変数で以下を設定:
//      - AzureAd:TenantId: Azure AD テナントID
//      - AzureAd:ClientId: アプリケーション（クライアント）ID
// ============================================================================

var azureAdSection = builder.Configuration.GetSection("AzureAd");
var isAzureAdConfigured = !string.IsNullOrEmpty(azureAdSection["ClientId"])
                          && azureAdSection["ClientId"] != "YOUR_CLIENT_ID";

if (isAzureAdConfigured)
{
    // Azure AD (Entra ID) 認証 - 本番環境
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(azureAdSection);

    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();
}
else
{
    // ダミー認証 - 開発環境（DevAuthenticationHandler を参照）
    // 全ユーザーを "Developer" として自動認証する
    builder.Services.AddAuthentication("Dev")
        .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>("Dev", null);
}

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// SignalR（Blazor Server）のタイムアウト設定
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
});

// Add Infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

// ヘルスチェック（Azure App Service 用）
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "db", "sql" });

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // サンプルデータは設定で制御（デフォルト: false、開発時のみtrueに設定可）
    var seedSampleData = builder.Configuration.GetValue<bool>("SeedSampleData");
    await DbSeeder.SeedAsync(context, seedSampleData);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

// 認証・認可ミドルウェア（常に有効）
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(QInfoRanker.Web.Client._Imports).Assembly);

// Microsoft Identity UI のエンドポイント（Azure AD 認証時のみ）
if (isAzureAdConfigured)
{
    app.MapControllers();
}

// SignalRハブのマッピング
app.MapHub<CollectionProgressHub>("/hubs/collection-progress");

// ヘルスチェックエンドポイント（Azure App Service 用、認証不要）
app.MapHealthChecks("/health", new HealthCheckOptions
{
    AllowCachingResponses = false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
}).AllowAnonymous();

app.Run();
