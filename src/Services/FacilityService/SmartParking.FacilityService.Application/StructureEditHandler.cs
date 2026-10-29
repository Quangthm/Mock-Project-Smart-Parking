using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application;

public static class StructureEditHandler
{
    public static void Apply(ParkingStructure structure, StructureEdit edit)
    {
        switch (edit.Action)
        {
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
            case "moveSlot":
                structure.MoveSlot(edit.ResourceId, edit.UnitId, edit.Code!);
                break;
            case "configureSlot":
                structure.ConfigureSlot(edit.ResourceId, edit.VehicleType, edit.SlotType, edit.Coordinates3D, edit.Features);
                break;
            case "removePath":
                structure.RemoveAccessPath(edit.ResourceId);
                break;
            case "backup":
                structure.ConfigureBackup(edit.UnitId == Guid.Empty ? null : edit.UnitId, edit.VehicleType, edit.Capacity);
                break;
            case "markBackup":
                structure.MarkBackup(edit.ResourceId, edit.Active);
                break;
            default:
                throw new StructureException("INVALID_STRUCTURE", "Unsupported structure action.");
        }
    }
}
