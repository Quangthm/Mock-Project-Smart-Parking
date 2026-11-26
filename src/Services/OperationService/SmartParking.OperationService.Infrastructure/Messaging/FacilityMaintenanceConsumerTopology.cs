namespace SmartParking.OperationService.Infrastructure.Messaging;

public static class FacilityMaintenanceConsumerTopology
{
    public const int CurrentSchemaVersion = 1;
    public const string ChangedExchangeName = "smartpark.facility.maintenance-changed.v1";
    public const string ChangedQueueName = "smartpark-operation-facility-maintenance-changed-v1";
}
