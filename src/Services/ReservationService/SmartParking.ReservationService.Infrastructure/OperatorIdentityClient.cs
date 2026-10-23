using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.ReservationService.Application;

namespace SmartParking.ReservationService.Infrastructure;

public sealed class OperatorIdentityClient(HttpClient client, IHttpContextAccessor context) : IOperatorIdentityClient
{
    public async Task<OperatorSiteScope> RequireSiteAsync(Guid siteId, CancellationToken ct)
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer))
            throw new ReservationGateException(401, "UNAUTHENTICATED", "Sign in as an assigned operator.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/operators/site-scope?siteId={siteId}");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ReservationGateException(401, "UNAUTHENTICATED", "Session is invalid or revoked.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ReservationGateException(403, "FORBIDDEN", "Operator access is required.");
            if (!response.IsSuccessStatusCode)
                throw new ReservationGateException(503, "IDENTITY_UNAVAILABLE", "Operator authority is unavailable.");

            var result = await response.Content.ReadFromJsonAsync<IdentityScope>(cancellationToken: ct);
            if (result is not { UserId: var userId, TenantIds: { Length: 1 } } ||
                userId == Guid.Empty || result.TenantIds[0] == Guid.Empty)
                throw new ReservationGateException(403, "FORBIDDEN", "This parking site is not assigned to the current operator.");

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
