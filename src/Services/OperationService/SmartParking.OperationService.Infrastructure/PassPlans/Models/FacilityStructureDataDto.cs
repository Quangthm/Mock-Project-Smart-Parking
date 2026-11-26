using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Infrastructure.PassPlans.Models;

internal sealed record FacilityStructureDataDto(
    [property: JsonPropertyName("site")] FacilityLotSummaryDto? Site,
    [property: JsonPropertyName("units")] IReadOnlyList<FacilityUnitItemDto>? Units,
    [property: JsonPropertyName("slots")] IReadOnlyList<FacilitySlotItemDto>? Slots,
    [property: JsonPropertyName("capacityViews")] IReadOnlyList<FacilityCapacityViewDto>? CapacityViews);
