namespace SmartParking.OperationService.API.Controllers;

public sealed record WalkInGateActionRequest(string PlateNumber, string? ImageData = null, string? VehicleType = null, Guid? SlotId = null);
