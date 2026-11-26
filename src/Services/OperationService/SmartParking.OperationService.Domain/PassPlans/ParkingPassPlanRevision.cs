namespace SmartParking.OperationService.Domain.PassPlans;

public sealed class ParkingPassPlanRevision
{
    public Guid Id { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public int RevisionNumber { get; private set; }
    public PassProductType ProductType { get; private set; }
    public Guid? ZoneId { get; private set; }
    public IReadOnlyList<PassVehicleType> VehicleTypes { get; private set; }
    public OrdinaryCoverageRules OrdinaryCoverageRules { get; private set; }
    public string ContractTimezone { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public string? PublishNote { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? IdempotencyPayloadHash { get; private set; }
    public Guid PublishedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ParkingPassPlanRevision(
        Guid id,
        Guid planId,
        Guid tenantId,
        Guid siteId,
        int revisionNumber,
        PassProductType productType,
        Guid? zoneId,
        IReadOnlyList<PassVehicleType> vehicleTypes,
        OrdinaryCoverageRules ordinaryCoverageRules,
        string contractTimezone,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string? publishNote,
        string? idempotencyKey,
        string? idempotencyPayloadHash,
        Guid publishedBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        PlanId = planId;
        TenantId = tenantId;
        SiteId = siteId;
        RevisionNumber = revisionNumber;
        ProductType = productType;
        ZoneId = zoneId;
        VehicleTypes = vehicleTypes;
        OrdinaryCoverageRules = ordinaryCoverageRules;
        ContractTimezone = contractTimezone;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        PublishNote = publishNote;
        IdempotencyKey = idempotencyKey;
        IdempotencyPayloadHash = idempotencyPayloadHash;
        PublishedBy = publishedBy;
        CreatedAt = createdAt;
    }

    public static ParkingPassPlanRevision CreatePublished(
        Guid planId,
        Guid tenantId,
        Guid siteId,
        int revisionNumber,
        PassProductType productType,
        PassPlanDraftSpec spec,
        string contractTimezone,
        string? publishNote,
        string? idempotencyKey,
        string? idempotencyPayloadHash,
        Guid publishedBy,
        DateTimeOffset now)
    {
        if (revisionNumber < 1)
            throw new PassPlanException(400, "VALIDATION_ERROR", "Revision number must be at least 1.");

        var tz = string.IsNullOrWhiteSpace(contractTimezone) ? "Asia/Ho_Chi_Minh" : contractTimezone.Trim();
        return new ParkingPassPlanRevision(
            Guid.NewGuid(),
            planId,
            tenantId,
            siteId,
            revisionNumber,
            productType,
            spec.ZoneId,
            spec.VehicleTypes.ToArray(),
            spec.OrdinaryCoverageRules,
            tz,
            now,
            null,
            string.IsNullOrWhiteSpace(publishNote) ? null : publishNote.Trim(),
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            idempotencyPayloadHash,
            publishedBy,
            now);
    }

    public static ParkingPassPlanRevision Rehydrate(
        Guid id,
        Guid planId,
        Guid tenantId,
        Guid siteId,
        int revisionNumber,
        PassProductType productType,
        Guid? zoneId,
        IReadOnlyList<PassVehicleType> vehicleTypes,
        OrdinaryCoverageRules ordinaryCoverageRules,
        string contractTimezone,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string? publishNote,
        string? idempotencyKey,
        string? idempotencyPayloadHash,
        Guid publishedBy,
        DateTimeOffset createdAt) =>
        new(
            id,
            planId,
            tenantId,
            siteId,
            revisionNumber,
            productType,
            zoneId,
            vehicleTypes,
            ordinaryCoverageRules,
            contractTimezone,
            effectiveFrom,
            effectiveTo,
            publishNote,
            idempotencyKey,
            idempotencyPayloadHash,
            publishedBy,
            createdAt);
}
