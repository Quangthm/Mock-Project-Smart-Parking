using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Infrastructure;

public sealed class OperatorSiteClient(HttpClient client, IHttpContextAccessor context) : IOperatorSiteAuthorizer
{
    public async Task<OwnerScope> ScopeAsync(Guid siteId, CancellationToken ct)
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer))
            throw new StructureException("UNAUTHENTICATED", "Sign in as an assigned Operator.");

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/operators/site-scope?siteId={siteId}");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new StructureException("UNAUTHENTICATED", "Session is invalid or revoked.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new StructureException("FORBIDDEN", "This parking site is not assigned to the current operator.");
            if (!response.IsSuccessStatusCode)
                throw new StructureException("IDENTITY_UNAVAILABLE", "Operator authority is unavailable.");

            var result = await response.Content.ReadFromJsonAsync<IdentityScope>(cancellationToken: ct);
            if (result is not { UserId: var userId, TenantIds: { Length: 1 } } ||
                userId == Guid.Empty || result.TenantIds[0] == Guid.Empty)
                throw new StructureException("IDENTITY_UNAVAILABLE", "Operator authority returned an invalid site scope.");
            return new(userId, result.TenantIds[0]);
        }
        catch (StructureException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new StructureException("IDENTITY_UNAVAILABLE", "Operator authority is unavailable.");
        }
    }

    private sealed record IdentityScope(Guid UserId, Guid[] TenantIds);
}
