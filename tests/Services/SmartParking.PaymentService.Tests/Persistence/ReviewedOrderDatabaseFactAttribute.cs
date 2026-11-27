namespace SmartParking.PaymentService.Tests.Persistence;

internal sealed class ReviewedOrderDatabaseFactAttribute : FactAttribute
{
    public ReviewedOrderDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_ORDER_TEST_DB")))
            Skip = "Requires a disposable PostgreSQL Payment database initialized from the owning schema.";
    }
}
