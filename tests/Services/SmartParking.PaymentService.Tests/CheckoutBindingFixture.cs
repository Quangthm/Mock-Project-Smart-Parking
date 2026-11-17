using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Infrastructure.Services;

namespace SmartParking.PaymentService.Tests;

internal sealed class CheckoutBindingFixture : ICheckoutBindingRepository
{
    private readonly ConcurrentDictionary<long, string> bindings = new();

    public static CheckoutSigningBindings Create() => new(new CheckoutBindingFixture(), new EphemeralDataProtectionProvider());

    public Task StoreIfAbsentAsync(long orderCode, string protectedBinding, CancellationToken cancellationToken,
        CheckoutBindingProvenance? provenance = null)
    {
        bindings.TryAdd(orderCode, protectedBinding);
        return Task.CompletedTask;
    }

    public Task<string?> ReadAsync(long orderCode, CancellationToken cancellationToken) =>
        Task.FromResult(bindings.TryGetValue(orderCode, out var value) ? value : null);

    public Task<string?> ReadLegacyAsync(long orderCode, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
