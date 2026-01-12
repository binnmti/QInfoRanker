using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using QInfoRanker.Infrastructure;
using QInfoRanker.Web;
using QInfoRanker.Infrastructure.Data;
using QInfoRanker.Web.Components;
using QInfoRanker.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Azure AD 認証設定
var azureAdSection = builder.Configuration.GetSection("AzureAd");
var isAuthConfigured = !string.IsNullOrEmpty(azureAdSection["ClientId"])
                       && azureAdSection["ClientId"] != "YOUR_CLIENT_ID";

if (isAuthConfigured)
{
    // 本番環境: Azure AD認証
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(azureAdSection);

    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();
}
else
{
    // 開発環境: ダミー認証（全員認証済みとして扱う）
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

// Microsoft Identity UI のエンドポイント
if (isAuthConfigured)
{
    app.MapControllers();
}

// SignalRハブのマッピング
app.MapHub<CollectionProgressHub>("/hubs/collection-progress");

app.Run();
