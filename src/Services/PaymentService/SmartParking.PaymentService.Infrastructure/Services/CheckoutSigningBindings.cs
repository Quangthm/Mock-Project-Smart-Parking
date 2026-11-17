using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Integrations;

namespace SmartParking.PaymentService.Infrastructure.Services;

public sealed class CheckoutSigningBindings(
    ICheckoutBindingRepository repository,
    IDataProtectionProvider protection) : ICheckoutSigningBindings
{
    private sealed record Binding(string Mode, int Version, string ChecksumKey);

    private IDataProtector Protector(long orderCode) => protection.CreateProtector(
        "SmartParking.PaymentService.CheckoutSigningBinding.v1", orderCode.ToString(CultureInfo.InvariantCulture));

    public async Task<bool> BindAsync(long orderCode, PaymentConfigurationSnapshot configuration, CancellationToken cancellationToken)
    {
        if (orderCode == 0 || string.IsNullOrWhiteSpace(configuration.ChecksumKey))
            return false;

        var binding = new Binding(configuration.Mode, configuration.Version, configuration.ChecksumKey);
        var protector = Protector(orderCode);
        await repository.StoreIfAbsentAsync(orderCode, protector.Protect(JsonSerializer.Serialize(binding)), cancellationToken);
        return await ReadBindingAsync(orderCode, cancellationToken) == binding;
    }

    public async Task<string?> ReadChecksumKeyAsync(long orderCode, CancellationToken cancellationToken) =>
        (await ReadBindingAsync(orderCode, cancellationToken))?.ChecksumKey;

    private async Task<Binding?> ReadBindingAsync(long orderCode, CancellationToken cancellationToken)
    {
        var value = await repository.ReadAsync(orderCode, cancellationToken);
        return value is null ? null : JsonSerializer.Deserialize<Binding>(Protector(orderCode).Unprotect(value));
    }
}
