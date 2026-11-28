namespace SmartParking.FacilityService.Tests;

public sealed class StructureRabbitMqFactAttribute : FactAttribute
{
    public StructureRabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_STRUCTURE_TEST_CONNECTION")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_RABBITMQ_TEST_CONNECTION")))
        {
            Skip = "Set SMARTPARK_STRUCTURE_TEST_CONNECTION and SMARTPARK_RABBITMQ_TEST_CONNECTION for isolated PostgreSQL + RabbitMQ verification.";
        }
    }
}
