using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Infrastructure.Clients;

public sealed class OperatorIdentityClient(HttpClient client, IHttpContextAccessor context) : IOperatorIdentityClient
{
    public Task<OperatorSiteScope> RequireSiteAsync(Guid siteId, CancellationToken ct)
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer))
            throw new ReservationGateException(401, "UNAUTHENTICATED", "Sign in as an assigned operator.");

        return ResolveSiteAsync(siteId, bearer, ct);
    }

    public Task<OperatorSiteScope> AuthorizeSiteAsync(Guid siteId, string bearerToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            throw new ReservationGateException(401, "UNAUTHENTICATED", "Sign in as an assigned operator.");

        var bearer = bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? bearerToken
            : $"Bearer {bearerToken}";
        return ResolveSiteAsync(siteId, bearer, ct);
    }

    private async Task<OperatorSiteScope> ResolveSiteAsync(Guid siteId, string bearer, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/operators/site-scope?siteId={siteId}");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ReservationGateException(401, "UNAUTHENTICATED", "Session is invalid or revoked.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ReservationGateException(403, "OPERATOR_LOT_UNASSIGNED", "This parking site is not assigned to the current operator.");
            if (!response.IsSuccessStatusCode)
                throw new ReservationGateException(503, "IDENTITY_UNAVAILABLE", "Operator authority is unavailable.");

            var result = await response.Content.ReadFromJsonAsync<IdentityScope>(cancellationToken: ct);
            if (result is not { UserId: var userId, TenantIds: { Length: 1 } } ||
                userId == Guid.Empty || result.TenantIds[0] == Guid.Empty)
                throw new ReservationGateException(403, "OPERATOR_LOT_UNASSIGNED", "This parking site is not assigned to the current operator.");

            return new(userId, result.TenantIds[0], siteId);
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ReservationGateException(503, "IDENTITY_UNAVAILABLE", "Operator authority is unavailable.");
        }
    }

    private sealed record IdentityScope(Guid UserId, Guid[] TenantIds);
}
