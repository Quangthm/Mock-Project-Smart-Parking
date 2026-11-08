using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

internal static class GateRequestValidation
{
    public static string? NormalizeVehicleType(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            null or "" => "CAR",
            "MOTORCYCLE" or "MOTORBIKE" => "MOTORCYCLE",
            "CAR" => "CAR",
            _ => null
        };

    public static void ValidatePlate(string? plate, string message)
    {
        if (string.IsNullOrWhiteSpace(plate) || plate.Length > 50)
            throw new ReservationGateException(400, "INVALID_PLATE", message);
    }

    public static void ValidateImageData(string? imageData)
    {
        if (imageData is not null &&
            (imageData.Length > 14 * 1024 * 1024 ||
             !(imageData.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
               imageData.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
               imageData.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase))))
            throw new ReservationGateException(400, "INVALID_IMAGE", "Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB.");
    }

    public static void ValidateReservationAction(string? plate, string? imageData)
    {
        if (plate is { Length: > 50 })
            throw new ReservationGateException(400, "INVALID_PLATE", "Plate number is too long.");
        ValidateImageData(imageData);
    }
}