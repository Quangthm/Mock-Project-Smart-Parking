using System.Buffers.Binary;
using System.Globalization;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Integrations;

// One deterministic contract shared by checkout issuance and binding persistence.
public static class PayOsOrderCodes
{
    public static long Resolve(string localOrderCode, Guid paymentOrderId)
    {
        if (long.TryParse(localOrderCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            if (numeric > 0) return numeric;
            throw new PaymentDomainException("INVALID_PAYMENT_ORDER_CODE", "The payment order code must be positive.");
        }
        if (paymentOrderId == Guid.Empty)
            throw new PaymentDomainException("INVALID_PAYMENT_ORDER_CODE", "A payment order identity is required.");

        // Preserve the existing little-endian Guid mapping, including a defined
        // unsigned magnitude for Int64.MinValue instead of Math.Abs overflow.
        var signed = BinaryPrimitives.ReadInt64LittleEndian(paymentOrderId.ToByteArray());
        var magnitude = signed < 0 ? (ulong)(-(signed + 1)) + 1 : (ulong)signed;
        return (long)(magnitude % 900000000000UL) + 100000000000L;
    }
}
