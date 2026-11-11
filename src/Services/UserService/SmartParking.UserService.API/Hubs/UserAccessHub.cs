using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.API.Hubs;

[Authorize]
public sealed class UserAccessHub(IAuthSessionStore sessions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actor) ||
            !Guid.TryParse(Context.User?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var token) ||
            !await sessions.IsActiveAsync(token, actor, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }
}
