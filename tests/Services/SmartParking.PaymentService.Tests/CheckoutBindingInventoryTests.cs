using System.Globalization;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Domain.Exceptions;
using Xunit;

namespace SmartParking.PaymentService.Tests;

public sealed class CheckoutBindingInventoryTests
{
    [Fact]
    public async Task UnsupportedRowsAreReported_AndFollowingValidRowsAreStillInspected()
    {
        var bindings = CheckoutBindingFixture.Create();
        Assert.True(await bindings.BindAsync(123, new(1, "LIVE", string.Empty, null, null, "synthetic-bound-key"), default));
        var candidates = new[]
        {
            new CheckoutBindingCandidate(Guid.NewGuid(), "historic/not-numeric", "link-1"),
            new CheckoutBindingCandidate(Guid.NewGuid(), "456", "link-2"),
            new CheckoutBindingCandidate(Guid.NewGuid(), "000123", "link-3"),
            new CheckoutBindingCandidate(Guid.NewGuid(), "123", "link-4")
        };
        var handler = new GetCheckoutBindingInventoryHandler(new Inventory(candidates), bindings);
        var rows = await handler.Handle(new(), default);
        Assert.Equal(4, rows.Count);
        Assert.Equal("UNSUPPORTED_ORDER_CODE", rows[0].State);
        Assert.Equal(candidates[0].PaymentOrderId, rows[0].PaymentOrderId);
        Assert.Equal("historic/not-numeric", rows[0].OriginalOrderCode);
        Assert.Null(rows[0].OrderCode);
        Assert.Equal("MISSING_BINDING", rows[1].State);
        Assert.Equal(456, rows[1].OrderCode);
        Assert.Equal("UNSUPPORTED_ORDER_CODE", rows[2].State);
        Assert.Null(rows[2].OrderCode);
        Assert.Equal("BOUND_LEGACY_POLICY", rows[3].State);
    }

    [Theory]
    [InlineData("123", "00112233-4455-6677-8899-aabbccddeeff")]
    [InlineData("legacy-code", "00112233-4455-6677-8899-aabbccddeeff")]
    [InlineData("overflow-legacy", "ffffffff-ffff-7fff-8899-aabbccddeeff")]
    public void CanonicalProviderCode_PreservesExistingIssuanceMapping(string localCode, string id)
    {
        var orderId = Guid.Parse(id);
        var expected = long.TryParse(localCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            ? numeric : Math.Abs(BitConverter.ToInt64(orderId.ToByteArray(), 0)) % 900000000000L + 100000000000L;
        Assert.Equal(expected, PayOsOrderCodes.Resolve(localCode, orderId));
    }

    [Fact]
    public void MinimumSignedGuidFragment_HasStablePositiveProviderCode()
    {
        var id = Guid.Parse("00000000-0000-8000-8899-aabbccddeeff");
        Assert.Equal(long.MinValue, BitConverter.ToInt64(id.ToByteArray(), 0));
        var code = PayOsOrderCodes.Resolve("legacy", id);
        Assert.InRange(code, 100000000000L, 999999999999L);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void NonpositiveProviderCodeIsRejected(string localCode) =>
        Assert.Throws<PaymentDomainException>(() => PayOsOrderCodes.Resolve(localCode, Guid.NewGuid()));

    private sealed class Inventory(IReadOnlyList<CheckoutBindingCandidate> rows) : ICheckoutBindingInventoryRepository
    {
        public Task<IReadOnlyList<CheckoutBindingCandidate>> ListAsync(int offset, int limit, CancellationToken ct) => Task.FromResult(rows);
    }
}
