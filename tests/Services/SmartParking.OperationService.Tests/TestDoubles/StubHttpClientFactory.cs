namespace SmartParking.OperationService.Tests.TestDoubles;

internal sealed class StubHttpClientFactory(HttpClient userClient, HttpClient facilityClient) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => name switch
    {
        "RealtimeUserAuthority" => userClient,
        "RealtimeFacilityAuthority" => facilityClient,
        _ => throw new InvalidOperationException($"Unexpected client name: {name}")
    };
}
