namespace SmartParking.OperationService.Tests.PassPlans;

internal sealed class PassPlanStubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastAuthorization { get; private set; }
    public bool HasServiceKeyHeader { get; private set; }
    public List<string> RequestedPaths { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastAuthorization = request.Headers.Authorization?.ToString();
        HasServiceKeyHeader = request.Headers.Contains("X-Service-Key");
        RequestedPaths.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
        return Task.FromResult(responder(request));
    }
}
