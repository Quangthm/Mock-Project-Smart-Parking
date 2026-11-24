namespace SmartParking.OperationService.Tests.TestDoubles;

// Records the last outbound request (and its body) so a test can assert the exact HTTP contract a
// client produced instead of only that "some" request was sent.
internal sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    private string? _body;

    public Task<string> LastBodyAsync() => Task.FromResult(_body ?? "");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            _body = await request.Content.ReadAsStringAsync(cancellationToken);
        return responder(request);
    }
}