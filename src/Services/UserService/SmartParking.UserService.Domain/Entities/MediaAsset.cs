namespace SmartParking.UserService.Domain.Entities;

/// <summary>One immutable upload. Provider success and business attachment are separate facts.</summary>
public sealed class MediaAsset
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CreatorAccountId { get; set; }
    public Guid ScopeId { get; set; }
    public string Purpose { get; set; } = "";
    public string Sensitivity { get; set; } = "restricted";
    public Guid? VehicleId { get; set; }
    public Guid? RegistrationClaimId { get; private set; }
    public Guid? DraftIntentId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long ByteCount { get; set; }
    public string Sha256 { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Provider { get; set; } = "cloudinary";
    public string Environment { get; set; } = "";
    public string PublicId { get; set; } = "";
    public string ResourceType { get; set; } = "image";
    public string DeliveryType { get; set; } = "authenticated";
    public string? ProviderAssetId { get; set; }
    public string? ProviderVersion { get; set; }
    public string State { get; private set; } = "pending";
    public long Version { get; private set; }
    public DateTimeOffset AdmittedAt { get; set; }
    public DateTimeOffset DraftExpiresAt { get; set; }
    public string RetentionClass { get; set; } = "image-30-days";
    public DateTimeOffset RetentionAnchor { get; set; }
    public DateTimeOffset RetainUntil { get; set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? AttachedAt { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public Guid? ClaimId { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? RetryAt { get; private set; }
    public string? RecoveryCode { get; private set; }

    public bool Expired(DateTimeOffset now) => now >= RetainUntil || AttachedAt is null && now >= DraftExpiresAt;
    public bool CanAttach(DateTimeOffset now, Guid? vehicleId) => State == "ready" && !Expired(now) && RegistrationClaimId is null
        && (Purpose == "avatar" && vehicleId is null || Purpose == "vehicle" && vehicleId is not null
            && (VehicleId is null || VehicleId == vehicleId));

    public bool TryClaim(DateTimeOffset now, Guid claimId)
    {
        if (State != "pending" || Expired(now) || LeaseUntil > now || Attempts >= 5 || RetryAt > now) return false;
        ClaimId = claimId;
        LeaseUntil = now.AddMinutes(2);
        Attempts++;
        Version++;
        return true;
    }

    public bool Complete(Guid claimId, string providerAssetId, string providerVersion, DateTimeOffset now)
    {
        if (State != "pending" || ClaimId != claimId || Expired(now)) return false;
        ProviderAssetId = providerAssetId;
        ProviderVersion = providerVersion;
        State = "ready";
        ReadyAt = now;
        ClaimId = null;
        LeaseUntil = null;
        RetryAt = null;
        RecoveryCode = null;
        Version++;
        return true;
    }

    public void Defer(Guid claimId, DateTimeOffset now, string code)
    {
        if (State != "pending" || ClaimId != claimId) return;
        ClaimId = null;
        LeaseUntil = null;
        RetryAt = now.AddMinutes(Math.Pow(2, Math.Min(Attempts, 5)));
        RecoveryCode = Attempts >= 5 ? "MANUAL_RECONCILIATION_REQUIRED" : code;
        Version++;
    }

    public void Attach(Guid? vehicleId, DateTimeOffset now)
    {
        if (!CanAttach(now, vehicleId)) throw new InvalidOperationException("Asset cannot be attached.");
        VehicleId = vehicleId;
        AttachedAt ??= now;
        Version++;
    }

    public void AttachToRegistrationClaim(Guid claimId, DateTimeOffset now)
    {
        if (claimId == Guid.Empty || Purpose != "vehicle" || VehicleId is not null
            || RegistrationClaimId is not null || AttachedAt is not null || State != "ready" || Expired(now))
            throw new InvalidOperationException("Asset cannot be attached to a registration claim.");
        RegistrationClaimId = claimId;
        AttachedAt = now;
        Version++;
    }
}

/// <summary>Independent preservation acknowledgements. No deletion worker is enabled in this slice.</summary>
public sealed class MediaHold
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string OriginatingHoldId { get; set; } = "";
    public Guid ActorAccountId { get; set; }
    public string Reason { get; set; } = "";
    public string Reference { get; set; } = "";
    public DateTimeOffset PlacedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public DateTimeOffset? ReviewAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public bool Active => AcknowledgedAt is not null && ReleasedAt is null;

    public void Acknowledge(DateTimeOffset now, bool deletionCommitted)
    {
        if (deletionCommitted || ReleasedAt is not null) throw new InvalidOperationException("Preservation is no longer possible.");
        AcknowledgedAt ??= now;
        ReviewAt = now.AddDays(30);
    }
    public void Release(DateTimeOffset now) => ReleasedAt ??= now;
}
