using System.Text.RegularExpressions;

namespace SmartParking.OperationService.Domain.Reservations;

public static class ReservationCarPlate
{
    // Same CAR grammar and allowed separators as UserService vehicle registration.
    public static string? Canonicalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var canonical = Regex.Replace(raw.Trim().ToUpperInvariant(), @"[\s.\-]", "");
        return Regex.IsMatch(canonical, @"^[1-9][0-9][A-Z][0-9]{5}$") ? canonical : null;
    }
}
