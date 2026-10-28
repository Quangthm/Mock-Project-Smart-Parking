using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Infrastructure;

// /api/auth/me is the only authoritative role source. Never parse the JWT locally.
public sealed class LayoutCallerResolver(
    HttpClient client,
    IHttpContextAccessor context,
    IOwnerAuthorizer owners,
    ISlotOperatorAuthorizer operators) : ILayoutCallerResolver
{
    public async Task<LayoutCaller> ResolveAsync(Guid siteId, CancellationToken ct)
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer))
            return new(LayoutCallerKind.Anonymous, null, [], false);

        var (userId, role) = await ReadIdentityAsync(bearer, ct);

        return role switch
        {
            "driver" => new(LayoutCallerKind.Driver, userId, [], false),
            "admin" => new(LayoutCallerKind.Admin, userId, [], true),
            "owner" => await ResolveOwnerAsync(userId, ct),
            "operator" => await ResolveOperatorAsync(userId, siteId, ct),
            _ => throw new StructureException("FORBIDDEN", "This role cannot access the operational layout.")
        };
    }

    private async Task<(Guid UserId, string Role)> ReadIdentityAsync(string bearer, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await client.SendAsync(request, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new StructureException("UNAUTHENTICATED", "Session is invalid or revoked.");
            if (!response.IsSuccessStatusCode)
                throw new StructureException("IDENTITY_UNAVAILABLE", "Identity service is unavailable.");

            var envelope = await response.Content.ReadFromJsonAsync<MeEnvelope>(ct);
            if (envelope is not { Success: true, Data: { } data } ||
                data.UserId == Guid.Empty ||
                string.IsNullOrWhiteSpace(data.Role))
                throw new StructureException("IDENTITY_UNAVAILABLE", "Invalid identity response.");

            return (data.UserId, data.Role.Trim().ToLowerInvariant());
        }
        catch (StructureException)
        {
            throw;
        }
        catch (Exception error) when (
            error is HttpRequestException or JsonException or NotSupportedException ||
            error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new StructureException("IDENTITY_UNAVAILABLE", "Identity service is unavailable.");
        }
    }

    private async Task<LayoutCaller> ResolveOwnerAsync(Guid userId, CancellationToken ct)
    {
        // IOwnerAuthorizer does not expose tenant scope; the registered implementation does.
        if (owners is not OwnerIdentityClient ownerIdentity)
            throw new StructureException("IDENTITY_UNAVAILABLE", "Owner scope authority is unavailable.");

        try
        {
            var scope = await ownerIdentity.ScopeAsync(ct);
            return new(LayoutCallerKind.Owner, userId, scope.TenantIds, true);
        }
        catch (StructureException error) when (error.Code == "FORBIDDEN")
        {
            return new(LayoutCallerKind.Owner, userId, [], false);
        }
    }

    private async Task<LayoutCaller> ResolveOperatorAsync(
        Guid userId, Guid siteId, CancellationToken ct)
    {
        try
        {
            var scope = await operators.ScopeAsync(siteId, ct);
            return new(LayoutCallerKind.Operator, userId, [scope.TenantId], true);
        }
        catch (StructureException error) when (error.Code == "FORBIDDEN")
        {
            return new(LayoutCallerKind.Operator, userId, [], false);
        }
    }

    private sealed record MeEnvelope(bool Success, MeData? Data);
    private sealed record MeData(Guid UserId, string? Role);
}
