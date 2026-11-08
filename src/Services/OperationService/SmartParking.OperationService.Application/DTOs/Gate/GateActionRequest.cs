namespace SmartParking.OperationService.Application.DTOs.Gate;

public sealed record GateActionRequest(string? PlateNumber, string? ImageData = null);