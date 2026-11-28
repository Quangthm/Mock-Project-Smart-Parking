using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record SetPassPlanEnablementRequest(
    [property: JsonPropertyName("isEnabled")] bool? IsEnabled,
    [property: JsonPropertyName("expectedPlanRevision")] int? ExpectedPlanRevision = null);
