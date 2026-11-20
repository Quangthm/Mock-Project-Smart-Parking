namespace SmartParking.OperationService.Tests;

internal sealed class PaymentAcknowledgementFault : DelegatingHandler
{
    public bool LoseNextOrderResponse { get; set; }
    public bool FailNextLookup { get; set; }
    public int FailedLookups { get; private set; }

    public PaymentAcknowledgementFault() : base(new HttpClientHandler()) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);
        if (LoseNextOrderResponse && request.Method == HttpMethod.Put && response.IsSuccessStatusCode)
        {
            LoseNextOrderResponse = false;
            response.Dispose();
            throw new HttpRequestException("Lost the committed order acknowledgement.");
        }
        if (FailNextLookup && request.RequestUri!.AbsolutePath.EndsWith("/collections", StringComparison.Ordinal))
        {
            FailNextLookup = false;
            FailedLookups++;
            response.Dispose();
            throw new HttpRequestException("First delivery cannot recover the collection.");
        }
        return response;
    }
}
