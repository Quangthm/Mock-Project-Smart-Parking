namespace SmartParking.OperationService.Domain.PassPlans;

public sealed class ParkingPassPlan
{
    public const string FixedEntitlementType = "WHEN_SPACE_AVAILABLE";

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public PassProductType ProductType { get; private set; }
    public string EntitlementType { get; private set; }
    public bool IsEnabled { get; private set; }
    public PassPlanStatus Status { get; private set; }
    public int ActiveRevision { get; private set; }
    public int? DraftRevision { get; private set; }
    public PassPlanDraftSpec? DraftSpec { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ParkingPassPlan(
        Guid id,
        Guid tenantId,
        Guid siteId,
        PassProductType productType,
        string entitlementType,
        bool isEnabled,
        PassPlanStatus status,
        int activeRevision,
        int? draftRevision,
        PassPlanDraftSpec? draftSpec,
        Guid createdBy,
        Guid updatedBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        SiteId = siteId;
        ProductType = productType;
        EntitlementType = entitlementType;
        IsEnabled = isEnabled;
        Status = status;
        ActiveRevision = activeRevision;
        DraftRevision = draftRevision;
        DraftSpec = draftSpec;
        CreatedBy = createdBy;
        UpdatedBy = updatedBy;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static ParkingPassPlan CreateDraft(
        Guid tenantId,
        Guid siteId,
        PassProductType productType,
        PassPlanDraftSpec spec,
        Guid actorId,
        DateTimeOffset now)
    {
        var normalized = NormalizeAndValidateDraft(spec);
        return new ParkingPassPlan(
            Guid.NewGuid(),
            tenantId,
            siteId,
            productType,
            FixedEntitlementType,
            false,
            PassPlanStatus.DRAFT,
            0,
            1,
            normalized,
            actorId,
            actorId,
            now,
            now);
    }

    public void SaveDraft(PassPlanDraftSpec spec, Guid actorId, DateTimeOffset now)
    {
        var normalized = NormalizeAndValidateDraft(spec);
        DraftSpec = normalized;
        DraftRevision = ActiveRevision + 1;
        if (ActiveRevision == 0)
        {
            Status = PassPlanStatus.DRAFT;
        }
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    public ParkingPassPlanRevision Publish(
        Guid actorId,
        string contractTimezone,
        string? publishNote,
        string? idempotencyKey,
        string? idempotencyPayloadHash,
        DateTimeOffset now)
    {
        if (DraftSpec is null)
        {
            throw new PassPlanException(
                404,
                "PASS_PLAN_DRAFT_NOT_FOUND",
                "No pass plan draft exists to publish.");
        }

        var nextRevision = ActiveRevision + 1;
        var revision = ParkingPassPlanRevision.CreatePublished(
            Id,
            TenantId,
            SiteId,
            nextRevision,
            ProductType,
            DraftSpec,
            contractTimezone,
            publishNote,
            idempotencyKey,
            idempotencyPayloadHash,
            actorId,
            now);

        ActiveRevision = nextRevision;
        DraftRevision = null;
        DraftSpec = null;
        Status = IsEnabled || ActiveRevision == 1
            ? PassPlanStatus.PUBLISHED
            : PassPlanStatus.DISABLED;
        UpdatedBy = actorId;
        UpdatedAt = now;

        return revision;
    }

    public void SetEnabled(
        bool isEnabled,
        int? expectedPlanRevision,
        Guid actorId,
        DateTimeOffset now)
    {
        if (isEnabled && ProductType == PassProductType.YEARLY)
        {
            throw new PassPlanException(
                422,
                "YEARLY_POLICY_BLOCKED_Q05",
                "Yearly parking products are blocked pending policy decision Q05.");
        }

        if (ActiveRevision <= 0)
        {
            throw new PassPlanException(
                422,
                "PLAN_NOT_PUBLISHED",
                "Publish a pass plan revision before changing enablement.");
        }

        if (expectedPlanRevision.HasValue && expectedPlanRevision.Value != ActiveRevision)
        {
            throw new PassPlanException(
                409,
                "PLAN_VERSION_CONFLICT",
                $"Expected plan revision {expectedPlanRevision.Value}, but current active revision is {ActiveRevision}.");
        }

        IsEnabled = isEnabled;
        Status = isEnabled ? PassPlanStatus.PUBLISHED : PassPlanStatus.DISABLED;
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    public static ParkingPassPlan Rehydrate(
        Guid id,
        Guid tenantId,
        Guid siteId,
        PassProductType productType,
        string entitlementType,
        bool isEnabled,
        PassPlanStatus status,
        int activeRevision,
        int? draftRevision,
        PassPlanDraftSpec? draftSpec,
        Guid createdBy,
        Guid updatedBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(
            id,
            tenantId,
            siteId,
            productType,
            entitlementType,
            isEnabled,
            status,
            activeRevision,
            draftRevision,
            draftSpec,
            createdBy,
            updatedBy,
            createdAt,
            updatedAt);

    private static PassPlanDraftSpec NormalizeAndValidateDraft(PassPlanDraftSpec spec)
    {
        if (!spec.IsAvailableOnly)
        {
            throw new PassPlanException(
                400,
                "VALIDATION_ERROR",
                "Pass plans must be available-only (WHEN_SPACE_AVAILABLE).",
                "isAvailableOnly");
        }

        if (spec.VehicleTypes is null || spec.VehicleTypes.Count == 0)
        {
            throw new PassPlanException(
                400,
                "VALIDATION_ERROR",
                "At least one supported vehicle type is required.",
                "vehicleTypes");
        }

        if (spec.OrdinaryCoverageRules is null ||
            (!spec.OrdinaryCoverageRules.DayCovered && !spec.OrdinaryCoverageRules.NightCovered))
        {
            throw new PassPlanException(
                400,
                "VALIDATION_ERROR",
                "At least one of dayCovered or nightCovered must be true.",
                "ordinaryCoverageRules");
        }

        if (spec.ZoneId.HasValue && spec.ZoneId.Value == Guid.Empty)
        {
            throw new PassPlanException(
                400,
                "VALIDATION_ERROR",
                "zoneId cannot be an empty GUID.",
                "zoneId");
        }

        var excludedZones = (spec.OrdinaryCoverageRules.ExcludedZoneIds ?? [])
            .Distinct()
            .ToArray();

        if (excludedZones.Any(z => z == Guid.Empty))
        {
            throw new PassPlanException(
                400,
                "VALIDATION_ERROR",
                "excludedZoneIds cannot contain an empty GUID.",
                "ordinaryCoverageRules.excludedZoneIds");
        }

        if (spec.ZoneId.HasValue && excludedZones.Contains(spec.ZoneId.Value))
        {
            throw new PassPlanException(
                400,
                "VALIDATION_ERROR",
                "zoneId cannot also be listed in excludedZoneIds.",
                "ordinaryCoverageRules.excludedZoneIds");
        }

        var vehicles = spec.VehicleTypes
            .Distinct()
            .OrderBy(v => v.ToString(), StringComparer.Ordinal)
            .ToArray();

        return new PassPlanDraftSpec(
            spec.ZoneId,
            vehicles,
            new OrdinaryCoverageRules(
                spec.OrdinaryCoverageRules.DayCovered,
                spec.OrdinaryCoverageRules.NightCovered,
                excludedZones),
            true);
    }
}
