using SmartParking.UserService.Domain.ValueObjects;

namespace SmartParking.UserService.Domain.Entities;

public sealed class RegisteredVehicle
{
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public string RawPlate { get; set; } = "";
    public string CanonicalPlate { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public string? ImageReference { get; set; }
    public Guid? ImageAssetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long BindingRevision { get; private set; } = 1;

    public static string Canonicalize(string rawPlate, string vehicleType) =>
        LicensePlateNormalizer.TryNormalize(rawPlate, vehicleType, out var canonical)
            ? canonical : throw new ArgumentException("Unsupported plate or vehicle category.");

    public static RegisteredVehicle Register(Guid accountId, string rawPlate, string vehicleType, DateTimeOffset now)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("An owning Account is required.");
        return new RegisteredVehicle { Id = Guid.NewGuid(), AccountId = accountId, RawPlate = rawPlate,
            CanonicalPlate = Canonicalize(rawPlate, vehicleType), VehicleType = vehicleType,
            CreatedAt = now, UpdatedAt = now };
    }

    public bool MatchesIdentity(string canonicalPlate, string vehicleType) =>
        CanonicalPlate == canonicalPlate && VehicleType == vehicleType;

    public void UpdateMetadata(string rawPlate, Guid imageAssetId, DateTimeOffset now)
    {
        if (DeletedAt is not null || imageAssetId == Guid.Empty
            || Canonicalize(rawPlate, VehicleType) != CanonicalPlate)
            throw new InvalidOperationException("Metadata changes cannot change the active vehicle identity.");
        RawPlate = rawPlate;
        AttachImage(imageAssetId, now);
    }

    public void AttachImage(Guid imageAssetId, DateTimeOffset now)
    {
        if (DeletedAt is not null || imageAssetId == Guid.Empty)
            throw new InvalidOperationException("An active vehicle and valid image are required.");
        ImageAssetId = imageAssetId;
        UpdatedAt = now;
    }
}
