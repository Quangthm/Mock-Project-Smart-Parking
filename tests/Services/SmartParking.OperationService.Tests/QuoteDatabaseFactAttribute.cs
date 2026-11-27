namespace SmartParking.OperationService.Tests;

public sealed class QuoteDatabaseFactAttribute : FactAttribute
{
    public QuoteDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_QUOTE_TEST_DB")))
            Skip = "Requires a freshly initialized disposable Operations PostgreSQL database.";
    }
}
