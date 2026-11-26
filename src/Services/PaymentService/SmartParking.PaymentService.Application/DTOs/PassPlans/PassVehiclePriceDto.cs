using System.Text.Json.Serialization;

namespace SmartParking.PaymentService.Application.DTOs.PassPlans;

public sealed record PassVehiclePriceDto(
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("fullPeriodPrice")] decimal FullPeriodPrice);
