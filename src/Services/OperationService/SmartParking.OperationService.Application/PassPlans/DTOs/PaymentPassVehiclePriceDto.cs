using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record PaymentPassVehiclePriceDto(
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("fullPeriodPrice")] decimal FullPeriodPrice);
