namespace SmartParking.OperationService.Tests;

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return response(request);
    }
}
