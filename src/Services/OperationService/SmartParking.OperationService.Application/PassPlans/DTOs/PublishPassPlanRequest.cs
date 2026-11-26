using System.Text.Json.Serialization;

namespace SmartParking.OperationService.Application.PassPlans.DTOs;

public sealed record PublishPassPlanRequest(
    [property: JsonPropertyName("publishNote")] string? PublishNote = null);
