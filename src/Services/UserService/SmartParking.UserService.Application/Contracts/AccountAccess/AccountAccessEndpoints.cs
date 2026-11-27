// Service-local wire contract; no source/project reference to another service.
// Namespace/type names are retained for MassTransit wire compatibility.
namespace SmartParking.Contracts.AccountAccess;

public static class AccountAccessEndpoints
{
    public const string Requests = "user-account-access-v1";
    public const string Decisions = "operations-account-access-results-v1";
}
