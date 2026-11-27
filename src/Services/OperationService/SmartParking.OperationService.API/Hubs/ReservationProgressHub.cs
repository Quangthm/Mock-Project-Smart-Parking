using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.API.Hubs;

[Authorize(Roles = "driver")]
public sealed class ReservationProgressHub(IMediator mediator) : Hub
{
    public async Task Subscribe(Guid requestId)
    {
        await mediator.Send(new ReadReservationReceiptQuery(requestId), Context.ConnectionAborted);
        var account = Guid.Parse(Context.User!.FindFirst("account_id")!.Value);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(account, requestId), Context.ConnectionAborted);
    }
    internal static string Group(Guid account, Guid request) => $"reservation:{account:D}:{request:D}";
}
