namespace UserService.Application.Usecase.Vehicles;

public sealed class SaveVehicleRequest
{
    public string Plate { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public Guid? ImageAssetId { get; set; }
    public Guid? ExpectedImageAssetId { get; set; }
}

public sealed record VehicleResponse(Guid Id, string RawPlate, string CanonicalPlate, string VehicleType,
    Guid? ImageAssetId, string? ImageReference, string ImageStatus, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record VehicleSaveOutcome(VehicleResponse? Vehicle);
public sealed record RegistrationClaimResponse(Guid Id, string RawPlate, string CanonicalPlate, string VehicleType,
    Guid ImageAssetId, string Status, DateTimeOffset CreatedAt);
public sealed record ResolveVehicleBindingRequest(Guid AccountId, Guid VehicleId, long? ExpectedRevision);
public sealed record BindingResponse(string State, Guid? VehicleId = null, Guid? AccountId = null,
    string? VehicleType = null, long? Revision = null, string? CanonicalPlate = null);
