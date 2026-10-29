using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using UserService.API.Common;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.API.Services;

/// <summary>Transport adapter for the shared Application current-access policy.</summary>
public sealed class CurrentAccountBearerEvents(ICurrentAccessService access, ApiErrorWriter errors) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            || !Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out var tokenId)
            || !Guid.TryParse(principal?.FindFirst("account_id")?.Value, out var accountId))
        {
            context.Fail("Session identity is invalid.");
            return;
        }
        try
        {
            await access.ValidateAsync(new TokenActor(userId, accountId, tokenId, principal?.FindFirst("role")?.Value),
                context.HttpContext.RequestAborted);
        }
        catch (AuthException) { context.Fail("Account access has changed."); }
    }

    public override async Task Challenge(JwtBearerChallengeContext context)
    {
        if (context.Handled || context.Response.HasStarted) return;
        context.HandleResponse();
        ApiResponseFactory.TryForStatus(StatusCodes.Status401Unauthorized, out var response);
        await errors.TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
    }

    public override async Task Forbidden(ForbiddenContext context)
    {
        if (context.Response.HasStarted) return;
        ApiResponseFactory.TryForStatus(StatusCodes.Status403Forbidden, out var response);
        await errors.TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
    }

    private sealed record TokenActor(Guid? UserId, Guid? AccountId, Guid? AccessTokenId, string? Role) : ICurrentActor;
}
