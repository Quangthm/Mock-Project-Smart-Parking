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

// Structure owns layout only. Occupancy, reservations and protection are separate inputs.
public sealed class ParkingStructure
{
    private readonly List<SpatialUnit> units;
    private readonly List<ParkingSlot> slots;
    private readonly List<AccessPath> paths;
    private readonly HashSet<Guid> protectedSlots;
    private readonly bool hasLiveCommitments;
    public SiteProfile Site { get; private set; }
    public IReadOnlyList<SpatialUnit> Units => units.AsReadOnly();
    public IReadOnlyList<ParkingSlot> Slots => slots.AsReadOnly();
    public IReadOnlyList<AccessPath> Paths => paths.AsReadOnly();

    public ParkingStructure(SiteProfile site, IEnumerable<SpatialUnit>? units = null,
        IEnumerable<ParkingSlot>? slots = null, IEnumerable<AccessPath>? paths = null,
        IEnumerable<Guid>? protectedSlots = null, bool hasLiveCommitments = false)
    {
        Site = site; this.units = units?.ToList() ?? []; this.slots = slots?.ToList() ?? [];
        this.paths = paths?.ToList() ?? []; this.protectedSlots = protectedSlots?.ToHashSet() ?? [];
        this.hasLiveCommitments = hasLiveCommitments;
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
    private void Recount() => Site = Site with { TotalPhysicalCapacity = slots.Count };

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
