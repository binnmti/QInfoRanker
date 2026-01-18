using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace QInfoRanker.Web;

/// <summary>
/// 開発環境用のダミー認証ハンドラー。
/// </summary>
/// <remarks>
/// <para>
/// <strong>目的:</strong>
/// Azure AD (Entra ID) 認証が設定されていない開発環境において、
/// 認証機能をスキップして全リクエストを認証済みとして扱います。
/// </para>
/// <para>
/// <strong>有効化条件:</strong>
/// appsettings.json の AzureAd:ClientId が未設定または "YOUR_CLIENT_ID" の場合に
/// Program.cs で自動的に登録されます。
/// </para>
/// <para>
/// <strong>注意:</strong>
/// このハンドラーは開発・テスト目的専用です。
/// 本番環境では必ず Azure AD 認証を設定してください。
/// </para>
/// </remarks>
public class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// 開発用ダミーユーザーの名前。
    /// </summary>
    private const string DevUserName = "Developer";

    /// <summary>
    /// 開発用ダミーユーザーのメールアドレス。
    /// </summary>
    private const string DevUserEmail = "dev@localhost";

    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// 認証処理を実行します。
    /// 開発環境では常に成功を返し、ダミーユーザーとして認証します。
    /// </summary>
    /// <returns>認証結果（常に成功）</returns>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, DevUserName),
            new Claim(ClaimTypes.Email, DevUserEmail),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
