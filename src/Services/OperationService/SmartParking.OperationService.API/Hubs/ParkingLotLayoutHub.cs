using Microsoft.AspNetCore.SignalR;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.API.Hubs;

public sealed class ParkingLotLayoutHub(IOperatorIdentityClient identity) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var siteIdRaw = http?.Request.RouteValues["siteId"]?.ToString();
        if (!Guid.TryParse(siteIdRaw, out var siteId)) throw new HubException("INVALID_SITE_ID");

        var token = http?.Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authHeader = http?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader)) token = authHeader;
        }
        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("UNAUTHENTICATED");

        try
        {
            await identity.AuthorizeSiteAsync(siteId, token, Context.ConnectionAborted);
        }
        catch (ReservationGateException ex) when (ex.StatusCode == 401 || ex.Code == "UNAUTHENTICATED")
        {
            throw new HubException("UNAUTHENTICATED");
        }
        catch (ReservationGateException ex) when (ex.StatusCode == 403)
        {
            throw new HubException("OPERATOR_LOT_UNASSIGNED");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"site:{siteId}:operators");
        await Clients.Caller.SendAsync("SnapshotRequired", siteId);
        await base.OnConnectedAsync();
    }
}
