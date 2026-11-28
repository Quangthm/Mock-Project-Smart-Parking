namespace SmartParking.FacilityService.Domain;

// Known parking models map to a fixed floor ceiling. Centralizing the mapping keeps the
// create-lot command and the owner-onboarding provisioning store deriving the same SiteProfile.MaxFloors.
public static class ParkingModelFloorCeilings
{
    public static int? Resolve(string? lotType, string? modelFileName)
    {
        var model = modelFileName?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(model))
        {
            if (model.StartsWith("outdoor_parking_lot", StringComparison.Ordinal)) return 1;
            if (model == "underground_parking_lot.glb") return 2;
            if (model == "indoor_parking_lot.glb") return 3;

            // Unrecognized custom model: the FE reads the real floor count from the uploaded
            // .glb, so no ceiling can be derived here and the requested MaxFloors is honored
            // (validated to be >= 1 downstream). Outdoor still carries an intrinsic one-floor rule.
            return lotType?.Trim().ToLowerInvariant() == "outdoor" ? 1 : null;
        }

        return lotType?.Trim().ToLowerInvariant() switch
        {
            "outdoor" => 1,
            "basement" => 2,
            "multi-storey" => 3,
            _ => null
        };
    }
}
