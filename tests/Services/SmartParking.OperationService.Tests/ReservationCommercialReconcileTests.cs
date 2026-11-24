using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.OperationService.Tests.TestDoubles;

namespace SmartParking.OperationService.Tests;

// Slice 3B / P3.4: the Operations -> PaymentService refund reconciliation call. The client must use
// the machine token, name the exact payment endpoint and refuse to treat a mismatched envelope as
// a successful refund.
public sealed class ReservationCommercialReconcileTests
{
    private static readonly Uri BaseAddress = new("http://localhost:5065/");

    [Fact]
    public async Task ReconcileAsync_PostsTheCollectionWithTheSignedOperationsToken()
    {
        using var rsa = RSA.Create(2048);
        var reservationId = Guid.NewGuid();
        var collection = NewCollection(reservationId);
        var handler = new RecordingHttpMessageHandler(_ => Json(Serialize(new
        {
            success = true,
            data = new
            {
                refundRequestId = Guid.NewGuid(),
                reservationId,
                collectionId = collection.CollectionId,
                status = "REFUND_REQUESTED",
                reason = "FACILITY_FAILURE",
                createdAt = DateTimeOffset.UtcNow
            }
        })));
        var client = CreateClient(handler, rsa.ExportRSAPrivateKeyPem());

        await client.ReconcileAsync(reservationId, collection, "FACILITY_FAILURE", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/internal/reservation-reconciliations", handler.LastRequest.RequestUri!.AbsolutePath);
        var authorization = handler.LastRequest.Headers.Authorization;
        Assert.Equal("Bearer", authorization?.Scheme);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(authorization!.Parameter);
        Assert.Equal("SmartParking.OperationService", token.Issuer);
        Assert.Contains("SmartParking.PaymentService", token.Audiences);
        Assert.Contains(token.Claims, claim => claim.Type == "permission" && claim.Value == "reservation:deposit-order");
        Assert.False(handler.LastRequest.Headers.Contains("X-Internal-Service-Key"));
        Assert.Equal(collection.AccountId.ToString(),
            handler.LastRequest.Headers.GetValues("X-Actor-AccountId").Single());
        Assert.Equal(collection.SiteId.ToString(),
            handler.LastRequest.Headers.GetValues("X-Actor-SiteId").Single());

        using var body = JsonDocument.Parse(await handler.LastBodyAsync());
        var root = body.RootElement;
        Assert.Equal(reservationId, root.GetProperty("reservationId").GetGuid());
        Assert.Equal(collection.CollectionId, root.GetProperty("collectionId").GetGuid());
        Assert.Equal(collection.Amount, root.GetProperty("amount").GetDecimal());
        Assert.Equal("FACILITY_FAILURE", root.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ReconcileAsync_RejectsIncompleteInputWithoutCallingPaymentService()
    {
        using var rsa = RSA.Create(2048);
        var handler = new RecordingHttpMessageHandler(_ =>
            throw new Xunit.Sdk.XunitException("HTTP must not be called for an invalid request."));
        var client = CreateClient(handler, rsa.ExportRSAPrivateKeyPem());
        var reservationId = Guid.NewGuid();
        var collection = NewCollection(reservationId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReconcileAsync(Guid.Empty, collection, "FACILITY_FAILURE", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.ReconcileAsync(reservationId, null!, "FACILITY_FAILURE", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReconcileAsync(reservationId, collection with { CollectionId = Guid.Empty },
                "FACILITY_FAILURE", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReconcileAsync(reservationId, collection with { Amount = 0m },
                "FACILITY_FAILURE", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReconcileAsync(reservationId, collection, "  ", CancellationToken.None));

        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [InlineData("reservation")]
    [InlineData("collection")]
    [InlineData("refund")]
    public async Task ReconcileAsync_FailsClosedWhenTheEnvelopeDoesNotMatchTheRequest(string tamper)
    {
        using var rsa = RSA.Create(2048);
        var reservationId = Guid.NewGuid();
        var collection = NewCollection(reservationId);
        var handler = new RecordingHttpMessageHandler(_ => Json(Serialize(new
        {
            success = true,
            data = new
            {
                refundRequestId = tamper == "refund" ? Guid.Empty : Guid.NewGuid(),
                reservationId = tamper == "reservation" ? Guid.NewGuid() : reservationId,
                collectionId = tamper == "collection" ? Guid.NewGuid() : collection.CollectionId,
                status = "REFUND_REQUESTED",
                reason = "FACILITY_FAILURE",
                createdAt = DateTimeOffset.UtcNow
            }
        })));
        var client = CreateClient(handler, rsa.ExportRSAPrivateKeyPem());

        var error = await Assert.ThrowsAsync<ReservationGateException>(() =>
            client.ReconcileAsync(reservationId, collection, "FACILITY_FAILURE", CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("OWNER_CONTRACT_INVALID", error.Code);
    }

    [Fact]
    public async Task ReconcileAsync_PropagatesPaymentRejections()
    {
        using var rsa = RSA.Create(2048);
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                Serialize(new { errorCode = "TRANSACTION_NOT_FOUND", message = "Collection is unknown." }),
                Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler, rsa.ExportRSAPrivateKeyPem());
        var reservationId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<ReservationGateException>(() =>
            client.ReconcileAsync(reservationId, NewCollection(reservationId), "FACILITY_FAILURE",
                CancellationToken.None));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("TRANSACTION_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task ReconcileAsync_ReportsUnavailableWhenPaymentServiceFails()
    {
        using var rsa = RSA.Create(2048);
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler, rsa.ExportRSAPrivateKeyPem());
        var reservationId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<ReservationGateException>(() =>
            client.ReconcileAsync(reservationId, NewCollection(reservationId), "FACILITY_FAILURE",
                CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("OWNER_UNAVAILABLE", error.Code);
    }

    private static VerifiedCollection NewCollection(Guid reservationId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), reservationId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "ops-v1", 10_000m, "VND");

    private static ReservationCommercialClient CreateClient(HttpMessageHandler handler, string privateKeyPem)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:AllowHttp"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = privateKeyPem
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ReservationCommercialClient(
            new HttpClient(handler) { BaseAddress = BaseAddress },
            new HttpContextAccessor(),
            configuration);
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}