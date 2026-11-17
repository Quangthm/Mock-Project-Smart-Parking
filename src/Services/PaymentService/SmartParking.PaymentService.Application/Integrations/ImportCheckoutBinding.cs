using System.Globalization;
using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Application.Integrations;

// Offline operator input only. Never expose through the Admin settings HTTP API or log this object.
public sealed class LegacyCheckoutBindingInput
{
    public long OrderCode { get; init; }
    public string ProviderLinkId { get; init; } = string.Empty;
    public string Source { get; init; } = "LEGACY_IMPORT";
    public string EvidenceReference { get; init; } = string.Empty;
    public string? Mode { get; init; }
    public int Version { get; init; }
    public string? ChecksumKey { get; init; }
}

public sealed record ImportCheckoutBindingCommand(LegacyCheckoutBindingInput Input, bool Apply)
    : IRequest<ImportCheckoutBindingResult>;
public sealed record ImportCheckoutBindingResult(long OrderCode, string State, string? ProviderLinkId = null);

public sealed class ImportCheckoutBindingHandler(IPaymentOrderRepository orders,
    IPaymentTransactionRepository transactions, ICheckoutSigningBindings bindings)
    : IRequestHandler<ImportCheckoutBindingCommand, ImportCheckoutBindingResult>
{
    public async Task<ImportCheckoutBindingResult> Handle(ImportCheckoutBindingCommand command, CancellationToken ct)
    {
        var input = command.Input;
        ImportCheckoutBindingResult Result(string state) => new(input.OrderCode, state);
        if (input.OrderCode <= 0 || input.Source is not ("LEGACY_IMPORT" or "LEGACY_POLICY") ||
            string.IsNullOrWhiteSpace(input.ProviderLinkId) || input.ProviderLinkId.Length > 255 ||
            string.IsNullOrWhiteSpace(input.EvidenceReference) || input.EvidenceReference.Length > 256 ||
            input.EvidenceReference.Any(char.IsControl)) return Result("INVALID_INPUT");

        if (input.Source == "LEGACY_IMPORT" && (input.Mode is not ("LIVE" or "SANDBOX") || input.Version <= 0 ||
            string.IsNullOrWhiteSpace(input.ChecksumKey) || input.ChecksumKey.Length > 1024 ||
            input.ChecksumKey.Any(char.IsControl))) return Result("INVALID_INPUT");

        var order = await orders.GetByOrderCodeAsync(input.OrderCode.ToString(CultureInfo.InvariantCulture), ct);
        if (order is null) return Result("ORDER_NOT_FOUND");
        var history = await transactions.GetByOrderIdAsync(order.Id, ct);
        if (!history.Any(transaction => transaction.PaymentOrderId == order.Id && transaction.TenantId == order.TenantId &&
            transaction.PaymentMethod == PaymentMethod.PAYOS && transaction.TransactionType == TransactionType.PAYMENT &&
            transaction.GatewayTransactionId == input.ProviderLinkId && !string.IsNullOrWhiteSpace(transaction.CheckoutUrl)))
            return Result("CHECKOUT_EVIDENCE_MISMATCH");

        if (input.Source == "LEGACY_POLICY")
        {
            var migrated = await bindings.MigrateLegacyAsync(input.OrderCode, input.EvidenceReference, command.Apply, ct);
            return Result(migrated switch { null => "LEGACY_BINDING_NOT_FOUND", false => "CONFLICT", true => command.Apply ? "APPLIED" : "READY" });
        }

        var configuration = new PaymentConfigurationSnapshot(input.Version, input.Mode!, string.Empty, null, null, input.ChecksumKey);
        if (await bindings.MatchesAsync(input.OrderCode, configuration, ct) is false) return Result("CONFLICT");
        if (!command.Apply) return Result("READY");
        var applied = await bindings.BindAsync(input.OrderCode, configuration, ct,
            new CheckoutBindingProvenance("LEGACY_IMPORT", input.EvidenceReference));
        return Result(applied ? "APPLIED" : "CONFLICT");
    }
}

public sealed record CheckoutBindingCandidate(string OrderCode, string ProviderLinkId);
public interface ICheckoutBindingInventoryRepository
{
    Task<IReadOnlyList<CheckoutBindingCandidate>> ListAsync(int offset, int limit, CancellationToken ct);
}

public sealed record GetCheckoutBindingInventoryQuery(int Offset = 0, int Limit = 100)
    : IRequest<IReadOnlyList<ImportCheckoutBindingResult>>;

public sealed class GetCheckoutBindingInventoryHandler(ICheckoutBindingInventoryRepository inventory,
    ICheckoutSigningBindings bindings) : IRequestHandler<GetCheckoutBindingInventoryQuery, IReadOnlyList<ImportCheckoutBindingResult>>
{
    public async Task<IReadOnlyList<ImportCheckoutBindingResult>> Handle(GetCheckoutBindingInventoryQuery request, CancellationToken ct)
    {
        if (request.Offset < 0 || request.Limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(request));
        var results = new List<ImportCheckoutBindingResult>();
        foreach (var candidate in await inventory.ListAsync(request.Offset, request.Limit, ct))
        {
            if (!long.TryParse(candidate.OrderCode, NumberStyles.None, CultureInfo.InvariantCulture, out var code) || code <= 0)
                throw new InvalidOperationException("Inventory contains an unsupported provider order code.");
            var key = await bindings.ReadChecksumKeyAsync(code, ct);
            results.Add(new(code, key is null ? "MISSING_BINDING" : "BOUND_LEGACY_POLICY", candidate.ProviderLinkId));
        }
        return results;
    }
}
