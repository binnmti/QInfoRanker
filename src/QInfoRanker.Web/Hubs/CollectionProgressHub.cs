using Microsoft.AspNetCore.SignalR;
using QInfoRanker.Core.Interfaces.Services;

namespace QInfoRanker.Web.Hubs;

/// <summary>
/// 収集進捗通知用SignalRハブ
/// 将来のリアルタイム通知用に準備
/// </summary>
public class CollectionProgressHub : Hub<ICollectionProgressClient>
{
    /// <summary>
    /// 特定キーワードの進捗を購読
    /// </summary>
    public async Task SubscribeToKeyword(int keywordId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"keyword-{keywordId}");
    }

    /// <summary>
    /// 特定キーワードの購読を解除
    /// </summary>
    public async Task UnsubscribeFromKeyword(int keywordId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"keyword-{keywordId}");
    }

    /// <summary>
    /// 全収集の進捗を購読
    /// </summary>
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-collections");
    }

    /// <summary>
    /// 全収集の購読を解除
    /// </summary>
    public async Task UnsubscribeFromAll()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-collections");
    }
}
