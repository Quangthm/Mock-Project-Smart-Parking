using SmartParking.ParkingService.Application;

namespace SmartParking.ParkingService.Tests;

public sealed class OperationalStateMappingTests
{
    [Theory]
    [InlineData("AVAILABLE", PhysicalState.AVAILABLE)]
    [InlineData("OCCUPIED", PhysicalState.OCCUPIED)]
    [InlineData("MAINTENANCE", PhysicalState.MAINTENANCE)]
    [InlineData("UNAVAILABLE", PhysicalState.UNAVAILABLE)]
    [InlineData("UNKNOWN", PhysicalState.UNKNOWN)]
    [InlineData(null, PhysicalState.UNKNOWN)]
    [InlineData("", PhysicalState.UNKNOWN)]
    [InlineData("   ", PhysicalState.UNKNOWN)]
    [InlineData("available", PhysicalState.UNKNOWN)]
    [InlineData("BOGUS", PhysicalState.UNKNOWN)]
    [InlineData("OPERATIONAL", PhysicalState.UNKNOWN)]
    [InlineData("BLOCKED", PhysicalState.UNKNOWN)]
    [InlineData(" AVAILABLE ", PhysicalState.AVAILABLE)]
    [InlineData("\tOCCUPIED\n", PhysicalState.OCCUPIED)]
    public void MapPhysicalStatePinsEveryBranch(string? raw, PhysicalState expected)
        => Assert.Equal(expected, OperationalStateMapping.MapPhysicalState(raw));

    [Theory]
    [InlineData(null, ReservationState.UNRESERVED)]
    [InlineData("", ReservationState.UNRESERVED)]
    [InlineData("   ", ReservationState.UNRESERVED)]
    [InlineData("UNRESERVED", ReservationState.UNRESERVED)]
    [InlineData("RESERVED", ReservationState.RESERVED)]
    [InlineData("PROTECTED", ReservationState.PROTECTED)]
    [InlineData("BACKUP", ReservationState.BACKUP)]
    [InlineData("reserved", ReservationState.BACKUP)]
    [InlineData("BOGUS", ReservationState.BACKUP)]
    [InlineData("HOLD", ReservationState.BACKUP)]
    [InlineData("0", ReservationState.BACKUP)]
    [InlineData(" RESERVED ", ReservationState.RESERVED)]
    [InlineData("\tPROTECTED\n", ReservationState.PROTECTED)]
    public void MapReservationStatePinsEveryBranch(string? raw, ReservationState expected)
        => Assert.Equal(expected, OperationalStateMapping.MapReservationState(raw));
}
