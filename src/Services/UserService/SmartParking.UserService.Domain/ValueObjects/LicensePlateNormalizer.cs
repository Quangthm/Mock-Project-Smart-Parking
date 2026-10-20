using System.Text.RegularExpressions;

namespace SmartParking.UserService.Domain.ValueObjects;

// The explicit MVP format contract, independent of persistence and HTTP.
// Special/historical series require an approved extension of this contract.
public static class LicensePlateNormalizer
{
    public static bool TryNormalize(string? raw, string type, out string canonical)
    {
        canonical = "";
        if (string.IsNullOrWhiteSpace(raw) || type is not ("CAR" or "MOTORCYCLE")) return false;
        var value = Regex.Replace(raw.Trim().ToUpperInvariant(), @"[\s.\-]", "");
        var pattern = type == "CAR" ? @"^[1-9][0-9][A-Z][0-9]{5}$" : @"^[1-9][0-9][A-Z][A-Z0-9][0-9]{5}$";
        if (!Regex.IsMatch(value, pattern)) return false;
        canonical = value;
        return true;
    }
}
