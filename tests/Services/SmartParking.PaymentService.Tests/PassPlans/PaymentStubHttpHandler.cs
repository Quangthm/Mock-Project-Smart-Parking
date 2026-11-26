namespace SmartParking.PaymentService.Tests.PassPlans;

internal sealed class PaymentStubHttpHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<string> RequestedPaths { get; } = [];
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        RequestedPaths.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
        return Task.FromResult(responder(request));
    }
}
