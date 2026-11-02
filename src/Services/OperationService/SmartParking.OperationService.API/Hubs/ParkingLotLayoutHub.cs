using System;
using Microsoft.AspNetCore.SignalR;

namespace SmartParking.OperationService.API.Hubs;

public sealed class ParkingLotLayoutHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var siteIdRaw = http?.Request.RouteValues["siteId"]?.ToString();
        if (!Guid.TryParse(siteIdRaw, out var siteId)) throw new HubException("INVALID_SITE_ID");

        var assigned = (Context.User?.FindFirst("AssignedLots")?.Value
                      ?? Context.User?.FindFirst("assigned_lots")?.Value
                      ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var isOverride = Context.User?.IsInRole("SLOT_OVERRIDE") == true
                      || Context.User?.HasClaim("permission", "SLOT_OVERRIDE") == true;
        if (!isOverride && !assigned.Contains(siteId.ToString(), StringComparer.OrdinalIgnoreCase))
            throw new HubException("OPERATOR_LOT_UNASSIGNED");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"site:{siteId}:operators");
        await Clients.Caller.SendAsync("SnapshotRequired", siteId);
        await base.OnConnectedAsync();
    }
}
