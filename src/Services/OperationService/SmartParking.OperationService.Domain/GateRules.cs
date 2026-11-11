namespace SmartParking.OperationService.Domain;

public sealed class GateTransitionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class GateRules
{
    public static string NormalizePlate(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    public static void VerifyPlate(string expected, string? recognized)
    {
        if (!string.IsNullOrWhiteSpace(recognized) && NormalizePlate(expected) != NormalizePlate(recognized))
            throw new GateTransitionException("PLATE_MISMATCH", "The scanned plate does not match this vehicle.");
    }
    public static string? NormalizeVehicleType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        null or "" or "CAR" => "CAR",
        "MOTORCYCLE" or "MOTORBIKE" => "MOTORCYCLE",
        _ => null
    };
}
