namespace SmartParking.ParkingService.Domain;

public enum UnitType { FLOOR, ZONE, BLOCK }
public enum VehicleType { CAR, MOTORCYCLE, OVERSIZED }
public enum SlotType { STANDARD, VIP, EV, DISABLED }
public enum OperationalStatus { OPERATIONAL, MAINTENANCE, UNKNOWN, BLOCKED }
public sealed class StructureException(string code, string message) : Exception(message)
{ public string Code { get; } = code; }

public sealed record SiteProfile(Guid Id, Guid TenantId, string Code, string Name, string Address,
    decimal? Latitude, decimal? Longitude, bool IsActive, int TotalPhysicalCapacity, string Status = "ACTIVE");
public sealed record SpatialUnit(Guid Id, Guid? ParentId, string Path, UnitType Type, string Name, int MaxCapacity);
public sealed record ParkingSlot(Guid Id, Guid UnitId, string Code, VehicleType VehicleType, SlotType Type,
    OperationalStatus OperationalStatus = OperationalStatus.OPERATIONAL, bool IsPhysicallyOccupied = false,
    string? Coordinates3D = null, string? Features = null, string? ReservationState = null);
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
    private readonly HashSet<Guid> protectedSlots;
    private readonly bool hasLiveCommitments;
    private readonly List<BackupPolicy> backupPolicies;
    private readonly CapacityClaim[] claims;
    public SiteProfile Site { get; private set; }
    public IReadOnlyList<SpatialUnit> Units => units.AsReadOnly();
    public IReadOnlyList<ParkingSlot> Slots => slots.AsReadOnly();
    public IReadOnlyList<AccessPath> Paths => paths.AsReadOnly();
    public IReadOnlyList<BackupPolicy> BackupPolicies => backupPolicies.AsReadOnly();
    public IReadOnlyList<CapacityView> CapacityViews => Views();

    public ParkingStructure(SiteProfile site, IEnumerable<SpatialUnit>? units = null,
        IEnumerable<ParkingSlot>? slots = null, IEnumerable<AccessPath>? paths = null,
        IEnumerable<Guid>? protectedSlots = null, bool hasLiveCommitments = false,
        IEnumerable<BackupPolicy>? backupPolicies = null, CapacityClaim[]? claims = null)
    {
        Site = site; this.units = units?.ToList() ?? []; this.slots = slots?.ToList() ?? [];
        this.paths = paths?.ToList() ?? []; this.protectedSlots = protectedSlots?.ToHashSet() ?? [];
        this.hasLiveCommitments = hasLiveCommitments;
        this.backupPolicies=backupPolicies?.ToList()??[];this.claims=claims??[];
    }

    private static void Require(bool valid, string message, string code = "INVALID_STRUCTURE")
    { if (!valid) throw new StructureException(code, message); }
    private static string Text(string value, int max)
    {
        Require(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max, $"Text must contain 1â€“{max} characters.");
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
    public Guid AddSlot(Guid unitId, string code, VehicleType vehicleType, SlotType type = SlotType.STANDARD)
    {
        var unit = Unit(unitId); code = Text(code, 50).ToUpperInvariant();
        Require(Enum.IsDefined(vehicleType) && Enum.IsDefined(type), "Unsupported vehicle or slot type.");
        Require(!slots.Any(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)), "Duplicate slot code in site.", "DUPLICATE_IDENTIFIER");
        CheckRoom(unit); var id = Guid.NewGuid(); slots.Add(new(id, unitId, code, vehicleType, type)); Recount(); return id;
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
    public void ConfigureSlot(Guid id, VehicleType vehicleType, SlotType type, string? coordinates3D, string? features)
    {
        var slot = Slot(id);
        Require(Enum.IsDefined(vehicleType) && Enum.IsDefined(type), "Unsupported vehicle or slot type.");
        Json(coordinates3D); Json(features); SafeChange([slot]);
        slots[slots.IndexOf(slot)] = slot with { VehicleType = vehicleType, Type = type, Coordinates3D = coordinates3D, Features = features };
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
}
