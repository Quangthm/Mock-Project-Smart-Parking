using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record PassFinancialReadinessDto(
    [property: JsonPropertyName("hasCompleteFinancialTerms")] bool HasCompleteFinancialTerms,
    [property: JsonPropertyName("financialTermId")] Guid? FinancialTermId,
    [property: JsonPropertyName("financialTermRevision")] int? FinancialTermRevision,
    [property: JsonPropertyName("blockedReason")] string? BlockedReason);
