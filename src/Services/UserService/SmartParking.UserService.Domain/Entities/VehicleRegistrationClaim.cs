namespace SmartParking.UserService.Domain.Entities;

public sealed class VehicleRegistrationClaim
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public string RawPlate { get; private set; } = "";
    public string CanonicalPlate { get; private set; } = "";
    public string VehicleType { get; private set; } = "";
    public Guid ImageAssetId { get; private set; }
    public string Status { get; private set; } = "PENDING_DISPUTE";
    public DateTimeOffset CreatedAt { get; private set; }

    public static VehicleRegistrationClaim Submit(Guid accountId, string rawPlate, string vehicleType,
        Guid imageAssetId, DateTimeOffset now)
    {
        if (accountId == Guid.Empty || imageAssetId == Guid.Empty)
            throw new ArgumentException("A claimant Account and image are required.");
        return new VehicleRegistrationClaim { Id = Guid.NewGuid(), AccountId = accountId,
            RawPlate = rawPlate, CanonicalPlate = RegisteredVehicle.Canonicalize(rawPlate, vehicleType),
            VehicleType = vehicleType, ImageAssetId = imageAssetId, CreatedAt = now };
    }
}
