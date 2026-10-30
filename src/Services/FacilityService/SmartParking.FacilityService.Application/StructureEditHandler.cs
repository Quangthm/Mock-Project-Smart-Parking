using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application;

public static class StructureEditHandler
{
    public static void Apply(ParkingStructure structure, StructureEdit edit)
    {
        switch (edit.Action)
        {
            case "batch":
                structure.ApplyBatch(edit.BatchItems
                    ?? throw new StructureException("INVALID_STRUCTURE", "A batch requires at least one item."));
                break;
            case "profile":
                structure.UpdateProfile(edit.Code!, edit.Name!, edit.Address!, edit.Latitude, edit.Longitude);
                break;
            case "active":
                structure.SetActive(edit.Active);
                break;
            case "renameUnit":
                structure.RenameUnit(edit.ResourceId, edit.Name!);
                break;
            case "capacity":
                structure.SetUnitCapacity(edit.ResourceId, edit.Capacity);
                break;
            case "removeUnit":
                structure.RemoveUnit(edit.ResourceId);
                break;
            case "removeSlot":
                structure.RemoveSlot(edit.ResourceId);
                break;
            case "restoreSlot":
                structure.RestoreSlot(edit.ResourceId);
                break;
            case "restoreUnit":
                structure.RestoreUnit(edit.ResourceId);
                break;
            case "restorePath":
                structure.RestorePath(edit.ResourceId);
                break;
            case "moveSlot":
                structure.MoveSlot(edit.ResourceId, edit.UnitId, edit.Code!);
                break;
            case "configureSlot":
                structure.ConfigureSlot(edit.ResourceId, Primary(edit), edit.SlotType, edit.Coordinates3D, edit.Features, edit.VehicleTypes);
                break;
            case "removePath":
                structure.RemoveAccessPath(edit.ResourceId);
                break;
            case "backup":
                structure.ConfigureBackup(edit.UnitId == Guid.Empty ? null : edit.UnitId,
                    edit.VehicleType ?? throw new StructureException("INVALID_STRUCTURE", "Vehicle type is required."), edit.Capacity);
                break;
            case "markBackup":
                structure.MarkBackup(edit.ResourceId, edit.Active);
                break;
            default:
                throw new StructureException("INVALID_STRUCTURE", "Unsupported structure action.");
        }
    }
    private static VehicleType Primary(StructureEdit edit)
        => SlotCompatibility.Primary(edit.VehicleType, edit.PrimaryVehicleType, edit.VehicleTypes);
}
