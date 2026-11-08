namespace SmartParking.OperationService.Application.DTOs.Gate;

public sealed record WalkInGateActionRequest(string PlateNumber, string? ImageData = null, string? VehicleType = null);