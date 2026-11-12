using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Infrastructure.Clients;

public sealed class OwnerIdentityClient(HttpClient client, IHttpContextAccessor context) : IOwnerIdentityClient
{
    public async Task<OwnerIdentityScope> GetOwnerScopeAsync(string permission = "PARKING_MANAGE", CancellationToken ct = default)
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer))
            throw new ReservationGateException(401, "UNAUTHORIZED", "Sign in as an owner.");

        if (!bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            bearer = $"Bearer {bearer}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/auth/owner-scope?permission={Uri.EscapeDataString(permission)}");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);

            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ReservationGateException(401, "UNAUTHORIZED", "Session is invalid or revoked.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new CrossLotAccessException("You do not have parking management permissions.");
            if (!response.IsSuccessStatusCode)
                throw new ReservationGateException(503, "IDENTITY_UNAVAILABLE", "Owner authority service is unavailable.");

            var result = await response.Content.ReadFromJsonAsync<IdentityScopeDto>(cancellationToken: ct);
            if (result is null || result.UserId == Guid.Empty)
                throw new CrossLotAccessException("Owner identity could not be verified.");

            return new OwnerIdentityScope(result.UserId, result.TenantIds ?? []);
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (CrossLotAccessException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ReservationGateException(503, "IDENTITY_UNAVAILABLE", "Owner authority service is unavailable.");
        }
    }

    private sealed record IdentityScopeDto(Guid UserId, Guid[]? TenantIds);
}
