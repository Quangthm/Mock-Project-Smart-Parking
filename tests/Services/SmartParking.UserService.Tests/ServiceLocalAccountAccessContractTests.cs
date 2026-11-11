using System.Text.Json;
using UserService.Application.Authorization;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.UserService.Tests;

public sealed class ServiceLocalAccountAccessContractTests
{
    [Theory]
    [InlineData("AccountAccessRequestV1")]
    [InlineData("AccountAccessDecisionV1")]
    public void IndependentlyOwnedContractsRetainWireIdentityAndRoundTrip(string name)
    {
        var wireName = "SmartParking.Contracts.AccountAccess." + name;
        var producer = typeof(EvaluateAccountCurrentHandler).Assembly.GetType(wireName, throwOnError: true)!;
        var consumer = typeof(AdmitReservationHandler).Assembly.GetType(wireName, throwOnError: true)!;
        Assert.NotEqual(producer.Assembly, consumer.Assembly);
        Assert.Equal(producer.FullName, consumer.FullName); // MassTransit namespace/type wire identity.
        static string[] Shape(Type type) => type.GetProperties()
            .Select(p => p.Name + ":" + p.PropertyType.FullName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(Shape(producer), Shape(consumer));
        const string payload = """
            {"WorkflowId":"10000000-0000-4000-8000-000000000001","AttemptId":"10000000-0000-4000-8000-000000000002",
             "DecisionId":"10000000-0000-4000-8000-000000000003","AttemptNumber":2,
             "UserId":"10000000-0000-4000-8000-000000000004","AccountId":"10000000-0000-4000-8000-000000000005",
             "Role":"driver","Operation":"CREATE_RESERVATION","AdmittedAt":"2026-11-09T00:00:00Z",
             "AttemptDeadline":"2026-11-09T00:00:20Z","OverallDeadline":"2026-11-09T00:01:30Z",
             "DecidedAt":"2026-11-09T00:00:01Z","Allowed":true,"Code":"ALLOWED"}
            """;
        var original = JsonSerializer.Deserialize(payload, producer)!;
        var outbound = JsonSerializer.Serialize(original, producer);
        var received = JsonSerializer.Deserialize(outbound, consumer)!;
        Assert.Equal(outbound, JsonSerializer.Serialize(received, consumer));
        var returned = JsonSerializer.Deserialize(JsonSerializer.Serialize(received, consumer), producer)!;
        Assert.Equal(outbound, JsonSerializer.Serialize(returned, producer));
    }
}
