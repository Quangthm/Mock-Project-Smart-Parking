using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record PaymentFinancialTermVehiclePriceHttpDto(
    [property: JsonPropertyName("vehicleType")] string VehicleType,
    [property: JsonPropertyName("fullPeriodPrice")] decimal FullPeriodPrice);
