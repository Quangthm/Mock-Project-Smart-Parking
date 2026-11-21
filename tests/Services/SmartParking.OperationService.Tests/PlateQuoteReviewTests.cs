using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class PlateQuoteReviewTests : ICurrentActor, IReservationPolicyRepository
{
    private readonly DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(2);
    private readonly Guid slot = Guid.NewGuid(), tenant = Guid.NewGuid(), site = Guid.NewGuid();
    private readonly Guid platformPolicy = Guid.NewGuid(), sitePolicy = Guid.NewGuid();
    public Guid? AccountId { get; } = Guid.NewGuid();
    public Guid? UserId { get; } = Guid.NewGuid();
    public string Role => "driver";

    public Task<IReadOnlyList<ReservationPolicyRevision>> ReadEffectiveAsync(Guid tenantId, Guid siteId,
        DateTimeOffset at, CancellationToken ct)
    {
        Assert.Equal((tenant, site), (tenantId, siteId));
        var values = ReservationPolicyResolver.RequiredKeys.ToDictionary(key => key, _ => 1);
        values["MAX_ADVANCE_BOOKING"] = 1440;
        values["RESERVATION_MAX_DURATION"] = 120;
        values["ARRIVAL_LATE_TOLERANCE"] = 15;
        return Task.FromResult<IReadOnlyList<ReservationPolicyRevision>>([
            new(platformPolicy, 1, null, null, values, new Dictionary<string, PolicyBound>(), null, "Asia/Ho_Chi_Minh"),
            new(sitePolicy, 2, tenant, site, new Dictionary<string, int>(), new Dictionary<string, PolicyBound>(), false, null)
        ]);
    }

    private ReservationQuoteBinding Expected => new(AccountId!.Value, "51A12345", tenant, site, slot,
        start, start.AddHours(1), "PAYOS", new(false, "none-v1"), "facility-rev", platformPolicy, 1, sitePolicy, 2);

    private PaymentReservationQuote Quote => new(Guid.NewGuid(), "quote-1", "ISSUED", start.AddMinutes(-15),
        Expected, "tariff-7", 25000, 0, 25000, "VND", "FULL", "Full prepayment");

    [Theory]
    [InlineData("51a-123.45", true)]
    [InlineData("51A 12345", true)]
    [InlineData("51A/12345", false)]
    [InlineData("51AB12345", false)]
    [InlineData("01A12345", false)]
    public void InputUsesRegistrationCarGrammar(string raw, bool valid)
    {
        var input = new ReviewReservationQuoteCommand(raw, slot, start, start.AddHours(1), "PAYOS");
        Assert.Equal(valid, new ReviewReservationQuoteValidator().Validate(input).IsValid);
        if (valid) Assert.Equal("51A12345", ReservationCarPlate.Canonicalize(raw));
    }

    [Theory]
    [InlineData("ok", null)]
    [InlineData("missing-slot", "SLOT_NOT_FOUND")]
    [InlineData("missing-facility-route", "OWNER_UNAVAILABLE")]
    [InlineData("ineligible", "SLOT_INELIGIBLE")]
    [InlineData("missing-payment-route", "OWNER_UNAVAILABLE")]
    [InlineData("expired", "QUOTE_STALE")]
    public async Task ReviewOnlyReadsFacilityAndIssuesOneBoundQuote(string scenario, string? errorCode)
    {
        var issued = Quote;
        using var rsa = RSA.Create(2048);
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem()
        }).Build();
        var facilityHttp = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"/internal/reservation-dependencies/v1/slots/{slot:D}", request.RequestUri!.AbsolutePath);
            if (scenario.StartsWith("missing-", StringComparison.Ordinal) && scenario != "missing-payment-route")
                return new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new
                    { code = scenario == "missing-slot" ? "SLOT_NOT_FOUND" : "NOT_FOUND" }) };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(
                new FacilityBookingFacts(tenant, site, slot, "facility-rev", scenario != "ineligible", true)) };
        });
        var paymentHttp = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/internal/reservation-quotes/v1", request.RequestUri!.AbsolutePath);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(request.Headers.Authorization!.Parameter);
            Assert.Contains(jwt.Claims, claim => claim.Type == "permission" && claim.Value == "reservation:quote");
            return scenario == "missing-payment-route" ? new(HttpStatusCode.NotFound)
                : new(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true,
                    data = scenario == "expired" ? issued with { ValidUntil = start.AddDays(-1) } : issued }) };
        });
        using var facilityClient = new HttpClient(facilityHttp) { BaseAddress = new("https://facility.test") };
        using var paymentClient = new HttpClient(paymentHttp) { BaseAddress = new("https://payment.test") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ReviewReservationQuoteHandler).Assembly));
        services.AddSingleton<IReservationPolicyRepository>(this);
        using var provider = services.BuildServiceProvider();
        var handler = new ReviewReservationQuoteHandler(this, new ReservationFacilityClient(facilityClient, settings),
            new ReservationCommercialClient(paymentClient, new HttpContextAccessor(), settings),
            new ReservationSubscriptionEligibility(), TimeProvider.System, provider.GetRequiredService<IMediator>());
        var input = new ReviewReservationQuoteCommand("51a-123.45", slot, start, start.AddHours(1), "PAYOS");
        if (errorCode is null)
        {
            var result = await handler.Handle(input, default);
            Assert.Equal(Expected, JsonSerializer.Deserialize<ReservationQuoteBinding>(
                paymentHttp.LastBody!, JsonSerializerOptions.Web));
            Assert.Equal(issued.QuoteId, result.QuoteId);
            Assert.False(result.IsCovered);
            Assert.Equal("none-v1", result.CoverageVersion);
            Assert.Equal("51A12345", result.CanonicalPlate);
            Assert.Equal(sitePolicy, result.OperationalPolicy.SiteRevisionId);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ReservationGateException>(() => handler.Handle(input, default));
            Assert.Equal(errorCode, error.Code);
            if (scenario is "missing-slot" or "missing-facility-route" or "ineligible")
                Assert.Null(paymentHttp.LastRequest);
        }
    }

    [Fact]
    public void AnyChangedSelectionOrCommercialEvidenceInvalidatesReview()
    {
        var expected = Expected;
        var quote = Quote;
        ReservationQuoteBinding[] changes = [
            expected with { AccountId = Guid.NewGuid() }, expected with { CanonicalPlate = "51B12345" },
            expected with { TenantId = Guid.NewGuid() }, expected with { SiteId = Guid.NewGuid() },
            expected with { RequestedSlotId = Guid.NewGuid() }, expected with { End = start.AddHours(2) },
            expected with { Start = start.AddMinutes(5) }, expected with { PaymentMethod = "CASH" },
            expected with { Coverage = new(true, "plan-2") }, expected with { FacilityVersion = "changed" },
            expected with { SitePolicyVersion = 3 }
        ];
        foreach (var changed in changes)
            Assert.Throws<ReservationGateException>(() => quote.ValidateFor(changed, start.AddHours(-1)));
        PaymentReservationQuote[] invalid = [
            quote with { Status = "ACCEPTED" }, quote with { QuoteId = Guid.Empty },
            quote with { QuoteVersion = "" }, quote with { TariffVersion = "" },
            quote with { Currency = "USD" }, quote with { CoveredAmount = 1 }, quote with { Disclosure = "" },
            quote with { DepositAmount = 0 }, quote with { DepositAmount = 1.25m }, quote with { BookedAmount = -1 }
        ];
        foreach (var mismatch in invalid)
            Assert.Throws<ReservationGateException>(() => mismatch.ValidateFor(expected, start.AddHours(-1)));
    }

    [Fact]
    public void MissingZeroValuedEvidenceCannotDeserializeAsNonCoverage()
    {
        var input = new ReviewReservationQuoteCommand("51A12345", slot, start, start.AddHours(1), "PAYOS");
        var form = JsonSerializer.SerializeToNode(input)!.AsObject();
        form["AccountId"] = Guid.NewGuid();
        Assert.Throws<JsonException>(() => form.Deserialize<ReviewReservationQuoteCommand>());
        var data = JsonSerializer.SerializeToNode(Quote)!.AsObject();
        data.Remove("CoveredAmount");
        Assert.Throws<JsonException>(() => data.Deserialize<PaymentReservationQuote>());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PlateCoverage>("{}"));
    }
}
