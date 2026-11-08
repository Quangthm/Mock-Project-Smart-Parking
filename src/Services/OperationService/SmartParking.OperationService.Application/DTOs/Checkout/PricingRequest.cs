namespace SmartParking.OperationService.Application.DTOs.Checkout;

public sealed record PricingRequest(
    Guid SiteId,
    Guid ReferenceId,
    string ReferenceType,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime);