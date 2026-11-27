using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationFacilityClient(HttpClient client, IConfiguration configuration)
    : IReservationFacilityOwner
{
    private const string Route = "internal/reservation-dependencies/v1/";
    private static readonly JsonSerializerOptions SlotReadJson = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };
    public async Task<SelectedSlotMetadata> ResolveSlotAsync(Guid slotId, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ReservationIntegration:Enabled")) throw Unavailable();
        if (client.BaseAddress is null || client.BaseAddress.Scheme != Uri.UriSchemeHttps &&
            !configuration.GetValue<bool>("ReservationIntegration:AllowHttp")) throw Unavailable();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Route}slots/{slotId:D}");
            request.Headers.Authorization = FacilityServiceToken.Create(configuration, "reservation:facility-dependency");
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var error = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var code) &&
                    code.ValueKind == JsonValueKind.String && code.GetString() == "SLOT_NOT_FOUND")
                    throw new ReservationGateException(404, "SLOT_NOT_FOUND", "The selected slot was not found.");
            }
            if (!response.IsSuccessStatusCode) throw Unavailable();
            return await response.Content.ReadFromJsonAsync<SelectedSlotMetadata>(SlotReadJson, ct) ?? throw Unavailable();
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (CryptographicException) { throw Unavailable(); }
        catch (IOException) { throw Unavailable(); }
        catch (UnauthorizedAccessException) { throw Unavailable(); }
        catch (ArgumentException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
    }
    private static ReservationGateException Unavailable() => new(503, "OWNER_UNAVAILABLE", "Facility dependency authority is unavailable.");
}
