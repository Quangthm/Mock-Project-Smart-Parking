using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;
using UserService.Application.Usecase.IdentityAccess;

namespace UserService.API.Hubs;

public sealed record AccessConnection(Guid Actor, Guid TokenId, Action Abort);
public sealed class AccessConnections
{
    public ConcurrentDictionary<string, AccessConnection> Items { get; } = new();
}

[Authorize(Roles = "owner")]
public sealed class OperatorAccessHub(AccessConnections connections, IAuthSessionStore sessions,
    IIdentityAccessRepository repository, ISender sender) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actor) ||
            !Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var token) ||
            !await sessions.IsActiveAsync(token, actor, Context.ConnectionAborted) ||
            (await repository.OwnerTenantsAsync(actor, "OPERATOR_MANAGE", Context.ConnectionAborted)).Length == 0)
        { Context.Abort(); return; }
        connections.Items[Context.ConnectionId] = new(actor, token, Context.Abort);
        await base.OnConnectedAsync();
    }
    // Clients must call on connection/reconnection. SignalR hints are never durable state.
    public async Task<IReadOnlyList<OperatorDto>> Reload()
    {
        if (!connections.Items.TryGetValue(Context.ConnectionId, out var connection) ||
            !await sessions.IsActiveAsync(connection.TokenId, connection.Actor, Context.ConnectionAborted))
        { Context.Abort(); throw new HubException("Session expired. Sign in again."); }
        return await sender.Send(new ListOperatorsQuery(connection.Actor), Context.ConnectionAborted);
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Items.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed class AccessChangeNotifier(AccessConnections connections, IHubContext<OperatorAccessHub> hub,
    IAuthSessionStore sessions, IIdentityAccessRepository repository) : IAccessChangeNotifier
{
    public async Task NotifyAsync(OperatorAccessChanged message, CancellationToken ct)
    {
        foreach (var (id, connection) in connections.Items)
        {
            if (!await sessions.IsActiveAsync(connection.TokenId, connection.Actor, ct))
            { connections.Items.TryRemove(id, out _); connection.Abort(); continue; }
            var tenants = await repository.OwnerTenantsAsync(connection.Actor, "OPERATOR_MANAGE", ct);
            if (tenants.Length == 0)
            { connections.Items.TryRemove(id, out _); connection.Abort(); continue; }
            if (message.TenantIds.Any(tenants.Contains))
                await hub.Clients.Client(id).SendAsync("OperatorsChanged", cancellationToken: ct);
        }
    }
}
