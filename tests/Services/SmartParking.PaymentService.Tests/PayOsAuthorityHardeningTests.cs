using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using Xunit;

namespace SmartParking.PaymentService.Tests;

public sealed class PayOsAuthorityHardeningTests
{
    private const long Code = 741852990;
    private const string CurrentKey = "synthetic-current-option-key";
    private static IOptions<PayOsOptions> OptionsValue() => Options.Create(new PayOsOptions
    {
        Mode = "LIVE", ClientId = "synthetic-client", ApiKey = "synthetic-api", ChecksumKey = CurrentKey,
        LiveClientId = "synthetic-client", LiveApiKey = "synthetic-api", LiveChecksumKey = CurrentKey
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionsOnly_RealCheckoutCannotBypassSettingsAndBinding(bool withBindings)
    {
        using var handler = new CountingHandler();
        using var client = new HttpClient(handler);
        var adapter = new PayOsAdapter(client, OptionsValue(), NullLogger<PayOsAdapter>.Instance,
            checkoutBindings: withBindings ? CheckoutBindingFixture.Create() : null);
        var result = await adapter.InitiatePaymentAsync(Request());
        Assert.False(result.IsSuccess);
        Assert.Equal("CHECKOUT_BINDING_UNAVAILABLE", result.ErrorCode);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionsCurrentKey_CannotValidateAnUnboundWebhook(bool withBindings)
    {
        var (payload, signature) = Payload(CurrentKey);
        var adapter = new PayOsAdapter(new HttpClient(), OptionsValue(), NullLogger<PayOsAdapter>.Instance,
            checkoutBindings: withBindings ? CheckoutBindingFixture.Create() : null);
        Assert.False((await adapter.VerifyAndParseWebhookAsync(payload, signature)).IsValidSignature);
    }

    [Fact]
    public async Task OptionsOnlyVerifier_TrustsExactBindingAndRejectsCurrentKey()
    {
        var bindings = CheckoutBindingFixture.Create();
        const string issuedKey = "synthetic-issued-before-rotation";
        Assert.True(await bindings.BindAsync(Code, new(1, "SANDBOX", string.Empty, null, null, issuedKey), default));
        var adapter = new PayOsAdapter(new HttpClient(), OptionsValue(), NullLogger<PayOsAdapter>.Instance, checkoutBindings: bindings);
        var issued = Payload(issuedKey);
        var current = Payload(CurrentKey);
        Assert.True((await adapter.VerifyAndParseWebhookAsync(issued.Json, issued.Signature)).IsValidSignature);
        Assert.False((await adapter.VerifyAndParseWebhookAsync(current.Json, current.Signature)).IsValidSignature);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionInfrastructureDi_RequiresBindingRegistrationForRealOutbound(bool removeBindings)
    {
        using var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(new ConfigurationBuilder().Build());
        services.AddSingleton<IPaymentIntegrationSettings>(new PaymentIntegrationSettingsStore(OptionsValue()));
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<ICheckoutBindingRepository>(new CheckoutBindingFixture());
        // Post-configure after the service's redirect-protection handler. Defaults
        // run first and can otherwise be overwritten by the named client setup.
        services.PostConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));
        if (removeBindings) services.RemoveAll<ICheckoutSigningBindings>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var adapter = scope.ServiceProvider.GetRequiredService<IPaymentGatewayPort>();
        Assert.IsType<PayOsAdapter>(adapter);
        var result = await adapter.InitiatePaymentAsync(Request());
        Assert.Equal(removeBindings ? 0 : 1, handler.Calls);
        if (removeBindings) Assert.Equal("CHECKOUT_BINDING_UNAVAILABLE", result.ErrorCode);
        else
        {
            Assert.NotNull(scope.ServiceProvider.GetService<ICheckoutSigningBindings>());
            Assert.Equal(CurrentKey, await scope.ServiceProvider.GetRequiredService<ICheckoutSigningBindings>().ReadChecksumKeyAsync(Code, default));
        }
    }

    private static PaymentInitiateRequest Request() => new(Guid.NewGuid(), Code, 25000, "authority regression",
        "https://example.invalid/return", "https://example.invalid/cancel", DateTimeOffset.UtcNow.AddMinutes(15));

    private static (string Json, string Signature) Payload(string key)
    {
        var data = new Dictionary<string, object?> { ["orderCode"] = Code, ["amount"] = 25000 };
        var signature = PayOsSignatureService.CreateSignatureFromDictionary(data, key);
        return (JsonSerializer.Serialize(new { code = "00", data, signature }), signature);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
