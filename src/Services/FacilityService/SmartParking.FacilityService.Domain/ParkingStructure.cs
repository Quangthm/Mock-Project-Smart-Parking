namespace SmartParking.FacilityService.Domain;

public enum UnitType { FLOOR, ZONE, BLOCK }
public enum VehicleType { CAR, MOTORCYCLE, OVERSIZED }
public enum SlotType { STANDARD, VIP, EV, DISABLED }
public enum OperationalStatus { OPERATIONAL, MAINTENANCE, UNKNOWN, BLOCKED }
public class StructureException(string code, string message) : Exception(message)
{ public string Code { get; } = code; }

// A create named an identifier that a soft-deleted row still owns. Restoring it is always explicit,
// so a delayed retry of an old create can never undo a deliberate removal.
public sealed class ReleasedIdentifierException(Guid resourceId, string releasedCode, DateTime deletedAt,
    string message = "This slot code belongs to a released slot; restore it explicitly.")
    : StructureException("IDENTIFIER_RELEASED", message)
{
    public Guid ResourceId { get; } = resourceId;
    // The identifier owned by the released row: slot code, unit name or path code.
    public string ReleasedCode { get; } = releasedCode;
    public DateTime DeletedAt { get; } = deletedAt;
}

public sealed record SiteProfile(Guid Id, Guid TenantId, string Code, string Name, string Address,
    decimal? Latitude, decimal? Longitude, bool IsActive, int TotalPhysicalCapacity, string Status = "ACTIVE");
public sealed record SpatialUnit(Guid Id, Guid? ParentId, string Path, UnitType Type, string Name, int MaxCapacity);
public sealed record ParkingSlot(Guid Id, Guid UnitId, string Code, VehicleType VehicleType, SlotType Type,
    OperationalStatus OperationalStatus = OperationalStatus.OPERATIONAL, bool IsPhysicallyOccupied = false,
    string? Coordinates3D = null, string? Features = null, string? ReservationState = null)
{
    // Categories this slot may be allocated to. VehicleType above stays the primary category: it is the
    // projection column and the only pool the slot is counted in, so compatibility never multiplies capacity.
    public IReadOnlyList<VehicleType> VehicleTypes { get; init; } = [];
}
public sealed record AccessPath(Guid Id, string Code, Guid? FromUnitId, Guid? ToUnitId, string? MapData);
public sealed record BackupPolicy(Guid? UnitId, VehicleType VehicleType, int Count);
public sealed record CapacityClaim(Guid? UnitId, string VehicleType, int PendingPayment, int Protected);
public sealed record CapacityView(Guid? UnitId, VehicleType VehicleType, int Total, int Occupied, int Protected,
    int PendingPayment, int ConfiguredBackup, int MarkedBackup, int EffectiveBackup, int Unavailable, int? Available, bool InheritedBackup=false);

// Structure owns layout only. Occupancy, reservations and protection are separate inputs.
public sealed class ParkingStructure
{
    private readonly List<SpatialUnit> units;
    private readonly List<ParkingSlot> slots;
    private readonly List<AccessPath> paths;
    // Soft-deleted rows stay here until an explicit restore moves them back. They are never live:
    // Recount, Scope, Views/CheckRoom and the public live collections all read only the lists above.
    private readonly List<ParkingSlot> releasedSlots;
    private readonly List<SpatialUnit> releasedUnits;
    private readonly List<AccessPath> releasedPaths;
    private readonly HashSet<Guid> protectedSlots;
    private readonly bool hasLiveCommitments;
    private readonly List<BackupPolicy> backupPolicies;
    private readonly CapacityClaim[] claims;
    // Version tokens loaded with the aggregate, used only to recheck expectedUpdatedAt at commit time.
    private readonly Dictionary<Guid, DateTime> versions;
    public SiteProfile Site { get; private set; }
    public IReadOnlyList<SpatialUnit> Units => units.AsReadOnly();
    public IReadOnlyList<ParkingSlot> Slots => slots.AsReadOnly();
    public IReadOnlyList<AccessPath> Paths => paths.AsReadOnly();
    // Soft-deleted rows are read-only restore candidates: they are never part of a live read, and no
    // response serializes the aggregate directly (the structure and operator-layout routes project
    // their fields explicitly).
    public IReadOnlyList<ParkingSlot> ReleasedSlots => releasedSlots.AsReadOnly();
    public IReadOnlyList<SpatialUnit> ReleasedUnits => releasedUnits.AsReadOnly();
    public IReadOnlyList<AccessPath> ReleasedPaths => releasedPaths.AsReadOnly();
    public IReadOnlyList<BackupPolicy> BackupPolicies => backupPolicies.AsReadOnly();
    public IReadOnlyList<CapacityView> CapacityViews => Views();

    public ParkingStructure(SiteProfile site, IEnumerable<SpatialUnit>? units = null,
        IEnumerable<ParkingSlot>? slots = null, IEnumerable<AccessPath>? paths = null,
        IEnumerable<Guid>? protectedSlots = null, bool hasLiveCommitments = false,
        IEnumerable<BackupPolicy>? backupPolicies = null, CapacityClaim[]? claims = null,
        IEnumerable<ParkingSlot>? releasedSlots = null, IEnumerable<SpatialUnit>? releasedUnits = null,
        IEnumerable<AccessPath>? releasedPaths = null, IReadOnlyDictionary<Guid, DateTime>? versions = null)
    {
        Site = site; this.units = units?.ToList() ?? []; this.slots = slots?.ToList() ?? [];
        this.paths = paths?.ToList() ?? []; this.protectedSlots = protectedSlots?.ToHashSet() ?? [];
        this.hasLiveCommitments = hasLiveCommitments;
        this.backupPolicies=backupPolicies?.ToList()??[];this.claims=claims??[];
        this.releasedSlots = releasedSlots?.ToList() ?? []; this.releasedUnits = releasedUnits?.ToList() ?? [];
        this.releasedPaths = releasedPaths?.ToList() ?? [];
        this.versions = versions?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [];
    }

    // The version the client last saw for a resource, when the load provided one.
    public DateTime? UpdatedAt(Guid id) => versions.TryGetValue(id, out var value) ? value : null;

    private static void Require(bool valid, string message, string code = "INVALID_STRUCTURE")
    { if (!valid) throw new StructureException(code, message); }
    private static string Text(string value, int max)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max, $"Text must contain 1Ă¢â‚¬â€œ{max} characters.");
        return value.Trim();
    }
    private SpatialUnit Unit(Guid id) => units.SingleOrDefault(u => u.Id == id)
        ?? throw new StructureException("UNIT_NOT_FOUND", "Spatial unit does not belong to this site.");
    private ParkingSlot Slot(Guid id) => slots.SingleOrDefault(s => s.Id == id)
        ?? throw new StructureException("SLOT_NOT_FOUND", "Slot does not belong to this site.");
    private void SafeChange(IEnumerable<ParkingSlot> affected)
    {
        Require(!hasLiveCommitments, "Resolve active/future reservations and capacity commitments before reducing or moving structure.", "STRUCTURE_IN_USE");
        Require(!affected.Any(s => s.IsPhysicallyOccupied || s.OperationalStatus == OperationalStatus.UNKNOWN || s.ReservationState != null || protectedSlots.Contains(s.Id)),
            "Occupied, unknown, allocated or protected slots cannot be removed or moved.", "STRUCTURE_IN_USE");
    }
    private void Recount() { Site = Site with { TotalPhysicalCapacity = slots.Count }; ValidateCapacity(); }
    private ParkingSlot[] Scope(Guid? unitId,VehicleType type)
    {
        var path=unitId is {} id?Unit(id).Path:null;
        return slots.Where(s=>s.VehicleType==type && (path is null || Unit(s.UnitId).Path.StartsWith(path,StringComparison.Ordinal))).ToArray();
    }
    private bool Contains(BackupPolicy policy,ParkingSlot slot)=>policy.UnitId is null || Unit(slot.UnitId).Path.StartsWith(Unit(policy.UnitId.Value).Path,StringComparison.Ordinal);
    private CapacityView View(Guid? unit,VehicleType type)
    {
        var scope=Scope(unit,type);var claim=claims.SingleOrDefault(c=>c.UnitId==unit && c.VehicleType==type.ToString());
        var occupied=scope.Count(s=>s.IsPhysicallyOccupied);
        var protectedCount=Math.Max(scope.Count(s=>!s.IsPhysicallyOccupied && (protectedSlots.Contains(s.Id)||s.ReservationState is "PROTECTED" or "RESERVED")),claim?.Protected??0);
        var unavailable=scope.Count(s=>!s.IsPhysicallyOccupied && !protectedSlots.Contains(s.Id) && s.ReservationState is not ("PROTECTED" or "RESERVED") && s.OperationalStatus!=OperationalStatus.OPERATIONAL);
        var marked=scope.Where(s=>s.ReservationState=="BACKUP").ToArray();
        var eligibleMarked=marked.Where(s=>!s.IsPhysicallyOccupied && !protectedSlots.Contains(s.Id) && s.OperationalStatus==OperationalStatus.OPERATIONAL).ToArray();
        var policies=backupPolicies.Where(p=>p.VehicleType==type && (unit is null || p.UnitId==unit || p.UnitId is {} id && Unit(id).Path.StartsWith(Unit(unit.Value).Path,StringComparison.Ordinal))).ToArray();
        // Ancestor BACKUP cannot be allocated to a descendant without an explicit policy split.
        if(unit is not null && backupPolicies.Any(p=>p.VehicleType==type && p.Count>0 && p.UnitId!=unit && (p.UnitId is null || Unit(unit.Value).Path.StartsWith(Unit(p.UnitId.Value).Path,StringComparison.Ordinal))))
            return new(unit,type,scope.Length,occupied,protectedCount,claim?.PendingPayment??0,0,marked.Length,eligibleMarked.Length,unavailable,null,true);
        var configured=policies.Sum(p=>p.Count);
        var effective=policies.Sum(p=>Math.Max(p.Count,eligibleMarked.Count(s=>Contains(p,s))))+eligibleMarked.Count(s=>!policies.Any(p=>Contains(p,s)));
        var available=scope.Length-occupied-protectedCount-(claim?.PendingPayment??0)-effective-unavailable;
        if(available<0)throw new StructureException("CAPACITY_CONFLICT","Capacity dimensions overlap or exceed physical capacity.");
        return new(unit,type,scope.Length,occupied,protectedCount,claim?.PendingPayment??0,configured,marked.Length,effective,unavailable,available);
    }
    private CapacityView[] Views()=>new Guid?[]{null}.Concat(units.Select(u=>(Guid?)u.Id)).SelectMany(id=>Enum.GetValues<VehicleType>().Select(t=>View(id,t))).ToArray();
    public void ValidateCapacity(){_ = Views();}
    public void ConfigureBackup(Guid? unitId,VehicleType type,int count)
    {
        Require(Enum.IsDefined(type) && count>=0,"Invalid backup type or count.");if(unitId is {} id)Unit(id);
        var old=backupPolicies.ToArray();backupPolicies.RemoveAll(p=>p.UnitId==unitId && p.VehicleType==type);
        try
        {
            if(count>0)
            {
                var path=unitId is {} u?Unit(u).Path:null;
                Require(!backupPolicies.Any(p=>p.VehicleType==type && p.Count>0 && (p.UnitId is null || path is null || Unit(p.UnitId.Value).Path.StartsWith(path,StringComparison.Ordinal)||path.StartsWith(Unit(p.UnitId.Value).Path,StringComparison.Ordinal))),"Split ancestor/descendant backup policies explicitly; scopes cannot overlap.");
                backupPolicies.Add(new(unitId,type,count));
            }
            ValidateCapacity();
        }
        catch {backupPolicies.Clear();backupPolicies.AddRange(old);throw;}
    }
    public void MarkBackup(Guid id,bool marked)
    {
        var slot=Slot(id);
        Require(!slot.IsPhysicallyOccupied && slot.OperationalStatus==OperationalStatus.OPERATIONAL && !protectedSlots.Contains(id) && slot.ReservationState is null or "BACKUP","Backup marking cannot overwrite physical truth or accepted protection.","STRUCTURE_IN_USE");
        slots[slots.IndexOf(slot)]=slot with{ReservationState=marked?"BACKUP":null};
        try{ValidateCapacity();}catch{slots[slots.FindIndex(s=>s.Id==id)]=slot;throw;}
    }

    public void UpdateProfile(string code, string name, string address, decimal? latitude, decimal? longitude)
    {
        code = Text(code, 50).ToUpperInvariant(); name = Text(name, 255); address = Text(address, 2000);
        Require(latitude.HasValue == longitude.HasValue && (latitude is null || latitude is >= -90 and <= 90)
            && (longitude is null || longitude is >= -180 and <= 180), "Provide a valid latitude/longitude pair.");
        Site = Site with { Code = code, Name = name, Address = address, Latitude = latitude, Longitude = longitude };
    }
    public void SetActive(bool active)
    { if (!active) SafeChange(slots); Site = Site with { IsActive = active, Status = active ? "ACTIVE" : "INACTIVE" }; }

    public Guid AddUnit(UnitType type, string name, Guid? parentId = null, int maxCapacity = 0)
    {
        Require(Enum.IsDefined(type) && maxCapacity >= 0, "Invalid unit type or capacity.");
        var parent = parentId is { } p ? Unit(p) : null;
        Require(parent is null || (int)type > (int)parent.Type, "Parent must precede the child in FLOOR/ZONE/BLOCK order.");
        name = Text(name, 100);
        Require(!units.Any(u => u.ParentId == parentId && u.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), "Duplicate sibling unit name.", "DUPLICATE_IDENTIFIER");
        var id = Guid.NewGuid();
        units.Add(new(id, parentId, $"{parent?.Path ?? $"/{Site.Id}/"}{id}/", type, name, maxCapacity));
        return id;
    }
    public void RenameUnit(Guid id, string name)
    {
        var unit = Unit(id); name = Text(name, 100);
        Require(!units.Any(u => u.Id != id && u.ParentId == unit.ParentId && u.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), "Duplicate sibling unit name.", "DUPLICATE_IDENTIFIER");
        units[units.IndexOf(unit)] = unit with { Name = name };
    }
    public void SetUnitCapacity(Guid id, int capacity)
    {
        var unit = Unit(id);
        Require(capacity >= 0 && (capacity == 0 || slots.Count(s => Unit(s.UnitId).Path.StartsWith(unit.Path, StringComparison.Ordinal)) <= capacity), "Capacity cannot be less than the existing physical slots; zero means no layout limit.");
        if ((unit.MaxCapacity == 0 && capacity > 0) || (capacity > 0 && capacity < unit.MaxCapacity)) SafeChange([]);
        units[units.IndexOf(unit)] = unit with { MaxCapacity = capacity };
    }
    public void RemoveUnit(Guid id)
    {
        var unit = Unit(id);
        Require(!units.Any(u => u.ParentId == id) && !slots.Any(s => s.UnitId == id) && !paths.Any(p => p.FromUnitId == id || p.ToUnitId == id),
            "Remove child units, slots and access paths first.", "UNIT_NOT_EMPTY");
        SafeChange([]); units.Remove(unit);
    }
    private void CheckRoom(SpatialUnit unit, Guid? excludedSlot = null)
    {
        foreach (var ancestor in units.Where(u => unit.Path.StartsWith(u.Path, StringComparison.Ordinal)))
            Require(ancestor.MaxCapacity == 0 || slots.Count(s => s.Id != excludedSlot && Unit(s.UnitId).Path.StartsWith(ancestor.Path, StringComparison.Ordinal)) < ancestor.MaxCapacity,
                "Spatial unit capacity exceeded.", "CAPACITY_EXCEEDED");
    }
    private static IReadOnlyList<VehicleType> Compatibility(VehicleType primary, IReadOnlyList<VehicleType>? vehicleTypes)
    {
        IReadOnlyList<VehicleType> values = vehicleTypes ?? [primary];
        Require(values.Count > 0 && values.All(value => Enum.IsDefined(value)) && values.Contains(primary),
            "Declare at least one vehicle type; the primary category must be one of them.");
        return values.Distinct().ToArray();
    }
    public Guid AddSlot(Guid unitId, string code, VehicleType vehicleType, SlotType type = SlotType.STANDARD,
        IReadOnlyList<VehicleType>? vehicleTypes = null)
    {
        var unit = Unit(unitId); code = Text(code, 50).ToUpperInvariant();
        Require(Enum.IsDefined(vehicleType) && Enum.IsDefined(type), "Unsupported vehicle or slot type.");
        var compatibility = Compatibility(vehicleType, vehicleTypes);
        Require(!slots.Any(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)), "Duplicate slot code in site.", "DUPLICATE_IDENTIFIER");
        CheckRoom(unit); var id = Guid.NewGuid();
        slots.Add(new(id, unitId, code, vehicleType, type) { VehicleTypes = compatibility }); Recount(); return id;
    }
    public void MoveSlot(Guid id, Guid targetUnitId, string code)
    {
        var slot = Slot(id); var unit = Unit(targetUnitId); code = Text(code, 50).ToUpperInvariant();
        Require(!slots.Any(s => s.Id != id && s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)), "Duplicate slot code in site.", "DUPLICATE_IDENTIFIER");
        SafeChange([slot]); CheckRoom(unit, id);
        slots[slots.IndexOf(slot)] = slot with { UnitId = targetUnitId, Code = code };
    }
    public void RemoveSlot(Guid id)
    { var slot = Slot(id); SafeChange([slot]); slots.Remove(slot); Recount(); }
    private static void Json(string? value)
    {
        if (value is null) return;
        Require(value.Length <= 65536, "Map/features data is too large.");
        try { using var doc = System.Text.Json.JsonDocument.Parse(value); }
        catch (System.Text.Json.JsonException) { throw new StructureException("INVALID_STRUCTURE", "Map/features data must be JSON."); }
    }
    public void ConfigureSlot(Guid id, VehicleType vehicleType, SlotType type, string? coordinates3D, string? features,
        IReadOnlyList<VehicleType>? vehicleTypes = null)
    {
        var slot = Slot(id);
        Require(Enum.IsDefined(vehicleType) && Enum.IsDefined(type), "Unsupported vehicle or slot type.");
        Require(coordinates3D is null && features is null, "Slot geometry comes from the lot's fixed 3D template.");
        var compatibility = Compatibility(vehicleType, vehicleTypes);
        SafeChange([slot]);
        // Geometry and features are server-owned: the loaded values pass through unchanged.
        slots[slots.IndexOf(slot)] = slot with { VehicleType = vehicleType, VehicleTypes = compatibility, Type = type };
    }
    public Guid AddAccessPath(string code, Guid? from, Guid? to, string? mapData = null)
    {
        code = Text(code, 50).ToUpperInvariant();
        Require(from != to || from is null, "Access path endpoints must differ.");
        if (from is { } f) Unit(f); if (to is { } t) Unit(t);
        Require(!paths.Any(p => p.Code.Equals(code, StringComparison.OrdinalIgnoreCase)), "Duplicate access path code.", "DUPLICATE_IDENTIFIER");
        Json(mapData);
        var id = Guid.NewGuid(); paths.Add(new(id, code, from, to, mapData)); return id;
    }
    public void RemoveAccessPath(Guid id)
    {
        var path = paths.SingleOrDefault(p => p.Id == id) ?? throw new StructureException("PATH_NOT_FOUND", "Access path was not found.");
        SafeChange(slots); paths.Remove(path);
    }

    // Restoration is explicit and keeps the row id, so allocation and audit history stay attached.
    public void RestoreSlot(Guid id)
    {
        if (slots.Any(s => s.Id == id)) return;
        var slot = releasedSlots.SingleOrDefault(s => s.Id == id)
            ?? throw new StructureException("SLOT_NOT_FOUND", "Slot does not belong to this site.");
        _ = Unit(slot.UnitId); // the owning unit must be live; a released unit answers UNIT_NOT_FOUND
        releasedSlots.Remove(slot);
        slots.Add(slot with { OperationalStatus = OperationalStatus.OPERATIONAL, IsPhysicallyOccupied = false, ReservationState = null });
        Recount();
    }
    public void RestoreUnit(Guid id)
    {
        if (units.Any(u => u.Id == id)) return;
        var unit = releasedUnits.SingleOrDefault(u => u.Id == id)
            ?? throw new StructureException("UNIT_NOT_FOUND", "Spatial unit does not belong to this site.");
        if (unit.ParentId is { } parentId) _ = Unit(parentId);
        Require(!units.Any(u => u.ParentId == unit.ParentId && u.Name.Equals(unit.Name, StringComparison.OrdinalIgnoreCase)),
            "Duplicate sibling unit name.", "DUPLICATE_IDENTIFIER");
        releasedUnits.Remove(unit);
        units.Add(unit); // the original path is kept so descendant references stay valid
        Recount();
    }
    public void RestorePath(Guid id)
    {
        if (paths.Any(p => p.Id == id)) return;
        var path = releasedPaths.SingleOrDefault(p => p.Id == id)
            ?? throw new StructureException("PATH_NOT_FOUND", "Access path was not found.");
        if (path.FromUnitId is { } from) _ = Unit(from);
        if (path.ToUnitId is { } to) _ = Unit(to);
        Require(!paths.Any(p => p.Code.Equals(path.Code, StringComparison.OrdinalIgnoreCase)), "Duplicate access path code.", "DUPLICATE_IDENTIFIER");
        releasedPaths.Remove(path);
        paths.Add(path);
    }

    // Same-lot, all-or-nothing deactivation. Every item is validated against the loaded structure
    // first (never the client's snapshot) and the whole aggregate is restored when any item fails, so
    // no selected resource is ever partially applied.
    public void ApplyBatch(IReadOnlyList<StructureBatchItem> items)
    {
        Require(items.Count is >= 1 and <= 50, "A batch must contain 1â€“50 items.");
        var failures = new List<StructureBatchFailure>();
        foreach (var item in items)
            if (BatchFailure(item) is { } failure) failures.Add(failure);
        if (failures.Count > 0) throw new StructureBatchException(failures);

        var snapshot = SnapshotState();
        foreach (var item in items)
        {
            try { ApplyBatchItem(item); }
            catch (StructureException error)
            {
                RestoreState(snapshot);
                throw new StructureBatchException([new(item.ResourceId, error.Code, error.Message)]);
            }
        }
    }
    private StructureBatchFailure? BatchFailure(StructureBatchItem item)
    {
        var found = item.Action switch
        {
            "active" => item.ResourceId == Site.Id,
            "removeUnit" => units.Any(unit => unit.Id == item.ResourceId),
            "removeSlot" or "markBackup" => slots.Any(slot => slot.Id == item.ResourceId),
            "removePath" => paths.Any(path => path.Id == item.ResourceId),
            _ => throw new StructureException("INVALID_STRUCTURE", "Unsupported batch action.")
        };
        // A foreign or missing id is reported as NOT_FOUND without naming the resource's details.
        if (!found) return new(item.ResourceId, "NOT_FOUND", "Resource is not part of this lot.");
        if (item.ExpectedUpdatedAt is { } expected && UpdatedAt(item.ResourceId) is { } actual && actual != expected)
            return new(item.ResourceId, "STRUCTURE_CONFLICT", "The resource changed after the batch was prepared; re-read the structure.");
        return null;
    }
    private void ApplyBatchItem(StructureBatchItem item)
    {
        switch (item.Action)
        {
            case "active": SetActive(false); break;
            case "removeUnit": RemoveUnit(item.ResourceId); break;
            case "removeSlot": RemoveSlot(item.ResourceId); break;
            case "removePath": RemoveAccessPath(item.ResourceId); break;
            case "markBackup": MarkBackup(item.ResourceId, true); break;
        }
    }
    private (SiteProfile Site, SpatialUnit[] Units, ParkingSlot[] Slots, AccessPath[] Paths, BackupPolicy[] Backups) SnapshotState()
        => (Site, [.. units], [.. slots], [.. paths], [.. backupPolicies]);
    private void RestoreState((SiteProfile Site, SpatialUnit[] Units, ParkingSlot[] Slots, AccessPath[] Paths, BackupPolicy[] Backups) snapshot)
    {
        Site = snapshot.Site;
        units.Clear(); units.AddRange(snapshot.Units);
        slots.Clear(); slots.AddRange(snapshot.Slots);
        paths.Clear(); paths.AddRange(snapshot.Paths);
        backupPolicies.Clear(); backupPolicies.AddRange(snapshot.Backups);
    }
}
