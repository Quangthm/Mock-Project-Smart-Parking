using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using Xunit;

namespace SmartParking.PaymentService.Tests;

public sealed class CheckoutBindingOutboundOrderingTests
{
    private const long OrderCode = 741852964;
    private const string Checksum = "synthetic-outbound-ordering-checksum";

    [Fact]
    public async Task Initiate_AwaitsStorage_AndBindingIsReadableWhenOutboundStarts()
    {
        var repository = new ControlledRepository();
        var protection = new EphemeralDataProtectionProvider();
        var bindings = new CheckoutSigningBindings(repository, protection);
        var bindingWasReadable = false;
        using var handler = new RecordingHandler(async ct =>
        {
            // A separate binding reader must recover the key at the outbound boundary.
            var reader = new CheckoutSigningBindings(repository, protection);
            Assert.Equal(Checksum, await reader.ReadChecksumKeyAsync(OrderCode, ct));
            bindingWasReadable = true;
        });
        using var client = new HttpClient(handler);
        var adapter = CreateAdapter(client, bindings);

        var pending = adapter.InitiatePaymentAsync(Request());
        try
        {
            await repository.StoreEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, handler.Calls);
            Assert.False(pending.IsCompleted);
        }
        finally
        {
            repository.ReleaseStore.TrySetResult();
        }
        await pending;
        Assert.Equal(1, handler.Calls);
        Assert.True(bindingWasReadable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Initiate_StorageOrReadbackFailure_DoesNotSendOutbound(bool failStore)
    {
        var repository = new ControlledRepository { FailStore = failStore, FailRead = !failStore };
        repository.ReleaseStore.SetResult();
        using var handler = new RecordingHandler(_ => Task.CompletedTask);
        using var client = new HttpClient(handler);
        var adapter = CreateAdapter(client, new CheckoutSigningBindings(repository, new EphemeralDataProtectionProvider()));

        var result = await adapter.InitiatePaymentAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("CHECKOUT_BINDING_UNAVAILABLE", result.ErrorCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Initiate_ExistingDifferentBinding_DoesNotSendOutboundOrReplaceKey()
    {
        var repository = new ControlledRepository();
        repository.ReleaseStore.SetResult();
        var bindings = new CheckoutSigningBindings(repository, new EphemeralDataProtectionProvider());
        var settings = Settings();
        Assert.True(await bindings.BindAsync(OrderCode, settings.GetCurrentSnapshot() with
            { ChecksumKey = "synthetic-earlier-checksum" }, default));
        using var handler = new RecordingHandler(_ => Task.CompletedTask);
        using var client = new HttpClient(handler);
        var adapter = CreateAdapter(client, bindings);

        var result = await adapter.InitiatePaymentAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("CHECKOUT_BINDING_CONFLICT", result.ErrorCode);
        Assert.Equal(0, handler.Calls);
        Assert.Equal("synthetic-earlier-checksum", await bindings.ReadChecksumKeyAsync(OrderCode, default));
    }

    private static PaymentIntegrationSettingsStore Settings() => new(Configuration());

    private static IOptions<PayOsOptions> Configuration() => Options.Create(new PayOsOptions
    {
        Mode = "LIVE",
        LiveClientId = "synthetic-client",
        LiveApiKey = "synthetic-api",
        LiveChecksumKey = Checksum
    });

    private static PayOsAdapter CreateAdapter(HttpClient client,
        ICheckoutSigningBindings bindings) =>
        new(client, Configuration(), NullLogger<PayOsAdapter>.Instance, Settings(), bindings);

    private static PaymentInitiateRequest Request() => new(Guid.NewGuid(), OrderCode, 25000,
        "ordering regression", "https://example.invalid/return", "https://example.invalid/cancel",
        DateTimeOffset.UtcNow.AddMinutes(15));

    private sealed class ControlledRepository : ICheckoutBindingRepository
    {
        private string? value;
        public TaskCompletionSource StoreEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseStore { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailStore { get; init; }
        public bool FailRead { get; init; }

        public async Task StoreIfAbsentAsync(long orderCode, string protectedBinding, CancellationToken cancellationToken,
            CheckoutBindingProvenance? provenance = null)
        {
            StoreEntered.TrySetResult();
            await ReleaseStore.Task.WaitAsync(cancellationToken);
            if (FailStore) throw new InvalidOperationException("Synthetic storage outage.");
            value ??= protectedBinding;
        }

        public Task<string?> ReadAsync(long orderCode, CancellationToken cancellationToken) =>
            FailRead && value is not null ? throw new InvalidOperationException("Synthetic readback outage.") : Task.FromResult(value);

        public Task<string?> ReadLegacyAsync(long orderCode, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private sealed class RecordingHandler(Func<CancellationToken, Task> atSend) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            await atSend(ct);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
    }
}
