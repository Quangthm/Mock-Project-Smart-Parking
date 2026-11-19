namespace SmartParking.Contracts.ReservationAdmission;

public sealed record PlateDecisionV1(PlateCheckV1 Request,
    [property: System.Text.Json.Serialization.JsonRequired] bool Eligible, string? VehicleType, long? Revision);
