using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TOD.Platform.AspNetCore.Authorization;

namespace STYS.Bildirimler.Hubs;

[Authorize(Policy = TodPlatformAuthorizationConstants.UiPolicy)]
public class BildirimHub : Hub
{
    public const string HubRoute = "/ui/bildirim-hub";
    public const string BildirimAlindiEventName = "bildirim-alindi";

    public override async Task OnConnectedAsync()
    {
        var userIdRaw = Context.User?.FindFirstValue("userId")
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userIdRaw, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
        }

        await base.OnConnectedAsync();
    }

    public static string GetUserGroupName(Guid userId)
    {
        return $"user:{userId:D}";
    }
}
