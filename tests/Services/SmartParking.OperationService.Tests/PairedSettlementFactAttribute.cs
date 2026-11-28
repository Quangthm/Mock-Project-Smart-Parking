namespace SmartParking.OperationService.Tests;

internal sealed class PairedSettlementFactAttribute : FactAttribute
{
    public PairedSettlementFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMARTPARK_SETTLEMENT_DB")))
            Skip = "Requires disposable Payment and Operations databases and a restricted test vhost.";
    }
}
