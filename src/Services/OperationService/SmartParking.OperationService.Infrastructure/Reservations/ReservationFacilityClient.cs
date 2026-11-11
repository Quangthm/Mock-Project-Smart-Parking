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
    : IReservationFacilityOwner, IReservationFacilityDependencyOwner
{
    private const string Route = "internal/reservation-dependencies/v1/";
    public Task<FacilityBookingFacts> ReadAsync(Guid siteId, Guid slotId, CancellationToken ct) =>
        SendAsync<FacilityBookingFacts>(HttpMethod.Get, $"{Route}sites/{siteId}/slots/{slotId}", null, ct);
    public Task<FacilityDependencyResult> AcquireAsync(FacilityDependencyIntent intent, CancellationToken ct) =>
        SendAsync<FacilityDependencyResult>(HttpMethod.Post, Route + "acquire", intent, ct);
    public Task<FacilityDependencyAcknowledgement> CompleteAsync(FacilityDependencyIntent intent, string outcomeJson, CancellationToken ct) =>
        SendAsync<FacilityDependencyAcknowledgement>(HttpMethod.Post, Route + "complete", new { intent, outcomeJson }, ct);
    public Task<FacilityDependencyAcknowledgement> AbortAsync(FacilityDependencyIntent intent, CancellationToken ct) =>
        SendAsync<FacilityDependencyAcknowledgement>(HttpMethod.Post, Route + "abort", intent, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string route, object? body, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("ReservationIntegration:Enabled")) throw Unavailable();
        if (client.BaseAddress is null || client.BaseAddress.Scheme != Uri.UriSchemeHttps &&
            !configuration.GetValue<bool>("ReservationIntegration:AllowHttp")) throw Unavailable();
        try
        {
            var pem = configuration["ReservationIntegration:OperationsPrivateKeyPem"];
            if (configuration["ReservationIntegration:OperationsPrivateKeyPath"] is { Length: > 0 } privatePath)
                pem = File.ReadAllText(privatePath);
            if (string.IsNullOrWhiteSpace(pem)) throw Unavailable();
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048) throw Unavailable();
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken("SmartParking.OperationService", "SmartParking.FacilityService",
                [new Claim("sub", "SmartParking.OperationService"), new Claim("sender", "SmartParking.OperationService"),
                 new Claim("permission", "reservation:facility-dependency"), new Claim("jti", Guid.NewGuid().ToString("N"))],
                now, now.AddMinutes(1), new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
            using var request = new HttpRequestMessage(method, route);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw Unavailable();
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
