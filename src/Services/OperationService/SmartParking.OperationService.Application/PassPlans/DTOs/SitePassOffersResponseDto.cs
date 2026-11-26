using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record SitePassOffersResponseDto(
    [property: JsonPropertyName("siteId")] Guid SiteId,
    [property: JsonPropertyName("timeZoneId")] string TimeZoneId,
    [property: JsonPropertyName("yearlyBlockedByQ05")] bool YearlyBlockedByQ05,
    [property: JsonPropertyName("offers")] IReadOnlyList<SitePassOfferDto> Offers);
