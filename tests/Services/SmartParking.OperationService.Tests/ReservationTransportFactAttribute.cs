namespace SmartParking.OperationService.Tests;

public sealed class ReservationTransportFactAttribute : FactAttribute
{
    public ReservationTransportFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_DB")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_BROKER_PORT")))
            Skip = "Requires an isolated Operations schema and RabbitMQ vhost.";
    }
}
