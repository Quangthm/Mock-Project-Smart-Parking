namespace SmartParking.PaymentService.Tests.Infrastructure;

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using Xunit;

public class ReservationSnapshotHttpAdapterTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private static ReservationSnapshotHttpAdapter CreateAdapter(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost:5002/")
        };
        return new ReservationSnapshotHttpAdapter(httpClient, NullLogger<ReservationSnapshotHttpAdapter>.Instance);
    }

    [Fact]
    public async Task GetAcceptedPolicySnapshotAsync_WithStringEnums_SuccessfullyReturnsDomainSnapshot()
    {
        // Arrange (Mô phỏng ca RES-HTTP-03 trong báo cáo QA PR #7)
        var json = """
        {
            "tariffId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
            "tariffName": "QA Standard Policy",
            "policyVersion": 4,
            "vehicleType": "CAR",
            "freeParkingGraceMinutes": 15,
            "timeBlocks": [
                {
                    "blockCode": "ALL_DAY",
                    "startTime": "00:00",
                    "endTime": "00:00",
                    "ratePerHourVnd": 5000
                }
            ],
            "timeZoneId": "Asia/Ho_Chi_Minh",
            "roundingMode": "HALF_UP_THOUSAND"
        }
        """;

        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await adapter.GetAcceptedPolicySnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"), result.TariffId);
        Assert.Equal("QA Standard Policy", result.TariffName);
        Assert.Equal(4, result.PolicyVersion);
        Assert.Equal(PricingVehicleType.CAR, result.VehicleType);
        Assert.Equal(15, result.FreeParkingGraceMinutes);
        Assert.Equal(MonetaryRoundingMode.HALF_UP_THOUSAND, result.RoundingMode);
        Assert.Single(result.TimeBlocks);
        Assert.Equal(5000L, result.TimeBlocks[0].RatePerHourVnd);
    }

    [Fact]
    public async Task GetAcceptedPolicySnapshotAsync_WithNumericEnums_SuccessfullyReturnsDomainSnapshot()
    {
        // Arrange (Mô phỏng ca đối chứng RES-ENUM-CTRL trong báo cáo QA PR #7)
        var json = """
        {
            "tariffId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
            "tariffName": "QA Control Numeric",
            "policyVersion": 4,
            "vehicleType": 0,
            "freeParkingGraceMinutes": 15,
            "timeBlocks": [
                {
                    "blockCode": "ALL_DAY",
                    "startTime": "00:00",
                    "endTime": "00:00",
                    "ratePerHourVnd": 5000
                }
            ],
            "timeZoneId": "Asia/Ho_Chi_Minh",
            "roundingMode": 2
        }
        """;

        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await adapter.GetAcceptedPolicySnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PricingVehicleType.CAR, result.VehicleType);
        Assert.Equal(MonetaryRoundingMode.HALF_UP_THOUSAND, result.RoundingMode);
    }

    [Fact]
    public async Task GetAcceptedPolicySnapshotAsync_WhenHttp404_ReturnsNull()
    {
        // Arrange
        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await adapter.GetAcceptedPolicySnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAcceptedPolicySnapshotAsync_WhenCorruptedJson_ThrowsCorruptedPolicySnapshotException()
    {
        // Arrange
        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ corrupt json ...", Encoding.UTF8, "application/json")
        });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PricingDomainException>(() =>
            adapter.GetAcceptedPolicySnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("CORRUPTED_POLICY_SNAPSHOT", ex.Code);
    }

    [Fact]
    public async Task GetAcceptedPolicySnapshotAsync_WhenInvalidVehicleTypeString_ThrowsCorruptedPolicySnapshotException()
    {
        // Arrange
        var json = """
        {
            "tariffId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
            "tariffName": "Invalid Vehicle Policy",
            "policyVersion": 1,
            "vehicleType": "UNKNOWN_SPACESHIP",
            "freeParkingGraceMinutes": 0,
            "timeBlocks": [
                {
                    "blockCode": "ALL_DAY",
                    "startTime": "00:00",
                    "endTime": "00:00",
                    "ratePerHourVnd": 10000
                }
            ]
        }
        """;

        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PricingDomainException>(() =>
            adapter.GetAcceptedPolicySnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("CORRUPTED_POLICY_SNAPSHOT", ex.Code);
    }

    [Fact]
    public async Task GetAcceptedPolicySnapshotAsync_WhenServiceUnavailable_ThrowsOperationServiceUnavailableException()
    {
        // Arrange
        var adapter = CreateAdapter(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PricingDomainException>(() =>
            adapter.GetAcceptedPolicySnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("RESERVATION_SERVICE_UNAVAILABLE", ex.Code);
    }
}
