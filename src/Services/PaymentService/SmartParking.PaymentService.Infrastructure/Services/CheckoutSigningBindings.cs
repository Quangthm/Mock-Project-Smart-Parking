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

    public async Task<bool> BindAsync(long orderCode, PaymentConfigurationSnapshot configuration, CancellationToken cancellationToken,
        CheckoutBindingProvenance? provenance = null)
    {
        if (orderCode <= 0 || configuration.Version <= 0 || configuration.Mode is not ("LIVE" or "SANDBOX") ||
            string.IsNullOrWhiteSpace(configuration.ChecksumKey))
            return false;

        var binding = new Binding(configuration.Mode, configuration.Version, configuration.ChecksumKey);
        // Honor existing authenticated legacy bindings before reserving the new storage row.
        // The INSERT/read-back below still arbitrates concurrent first writers.
        var existing = await ReadBindingAsync(orderCode, cancellationToken);
        if (existing is not null && existing != binding) return false;
        var protector = Protector(orderCode);
        await repository.StoreIfAbsentAsync(orderCode, protector.Protect(JsonSerializer.Serialize(binding)), cancellationToken, provenance);
        return await ReadBindingAsync(orderCode, cancellationToken) == binding;
    }

    public async Task<string?> ReadChecksumKeyAsync(long orderCode, CancellationToken cancellationToken) =>
        (await ReadBindingAsync(orderCode, cancellationToken))?.ChecksumKey;

    public async Task<bool?> MatchesAsync(long orderCode, PaymentConfigurationSnapshot configuration, CancellationToken cancellationToken)
    {
        var existing = await ReadBindingAsync(orderCode, cancellationToken);
        return existing is null ? null : existing == new Binding(configuration.Mode, configuration.Version, configuration.ChecksumKey ?? string.Empty);
    }

    public async Task<bool?> MigrateLegacyAsync(long orderCode, string evidenceReference, bool apply, CancellationToken cancellationToken)
    {
        var payload = await repository.ReadLegacyAsync(orderCode, cancellationToken);
        if (payload is null) return null;
        var legacy = Decode(orderCode, payload);
        var configuration = new PaymentConfigurationSnapshot(legacy.Version, legacy.Mode, string.Empty, null, null, legacy.ChecksumKey);
        if (await MatchesAsync(orderCode, configuration, cancellationToken) is false) return false;
        return !apply || await BindAsync(orderCode, configuration, cancellationToken,
            new CheckoutBindingProvenance("LEGACY_POLICY", evidenceReference));
    }

    private async Task<Binding?> ReadBindingAsync(long orderCode, CancellationToken cancellationToken)
    {
        var value = await repository.ReadAsync(orderCode, cancellationToken);
        return value is null ? null : Decode(orderCode, value);
    }

    private Binding Decode(long orderCode, string value)
    {
        var binding = JsonSerializer.Deserialize<Binding>(Protector(orderCode).Unprotect(value));
        if (binding is null || binding.Mode is not ("LIVE" or "SANDBOX") || binding.Version <= 0 ||
            string.IsNullOrWhiteSpace(binding.ChecksumKey)) throw new InvalidOperationException("Invalid protected checkout binding.");
        return binding;
    }
}
