namespace SmartParking.OperationService.Infrastructure.Messaging;

// Exchange and queue names must match the Facility publisher exactly: Slice 3A publishes on the
// fanout exchanges below, so this consumer binds its own durable queue to the same names.
public static class FacilityCapacityRestrictionConsumerTopology
{
    public const int CurrentSchemaVersion = 1;
    public const string RestrictedExchangeName = "smartpark.facility.capacity-restricted.v1";
    public const string ReleasedExchangeName = "smartpark.facility.capacity-restriction-released.v1";
    public const string RestrictedQueueName = "smartpark-operation-facility-capacity-restricted-v1";
    public const string ReleasedQueueName = "smartpark-operation-facility-capacity-restriction-released-v1";
}