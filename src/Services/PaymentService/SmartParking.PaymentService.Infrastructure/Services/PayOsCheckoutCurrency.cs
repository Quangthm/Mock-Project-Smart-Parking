using System.Globalization;

namespace SmartParking.PaymentService.Infrastructure.Services;

internal static class PayOsCheckoutCurrency
{
    // Merchant-presented VietQR stores the ISO 4217 currency in top-level tag 53.
    public static bool IsVnd(string qr)
    {
        if (!qr.StartsWith("000201010212", StringComparison.Ordinal))
            return false;

        var currencyFound = false;
        var fields = qr.AsSpan();
        while (fields.Length >= 4)
        {
            var tag = fields[..2];
            if (!int.TryParse(tag, NumberStyles.None, CultureInfo.InvariantCulture, out _) ||
                !int.TryParse(fields.Slice(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var length) ||
                length == 0 || length > fields.Length - 4)
                return false;

            var value = fields.Slice(4, length);
            fields = fields[(length + 4)..];
            if (tag.SequenceEqual("53"))
            {
                if (currencyFound || !value.SequenceEqual("704"))
                    return false;
                currencyFound = true;
            }
            if (tag.SequenceEqual("63"))
                return currencyFound && length == 4 && fields.IsEmpty &&
                    ushort.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
        }
        return false;
    }
}
