using System.Security.Claims;

namespace SmartParking.OperationService.API.Authentication;

public static class PassPlanOwnerPolicy
{
    public const string Name = "OwnerLotManagement";

    public static IServiceCollection AddPassPlanOwnerPolicy(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(Name, policy =>
            policy.RequireAuthenticatedUser()
                .RequireAssertion(context =>
                {
                    var role = context.User.FindFirst("role")?.Value
                        ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
                    return string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(role, "BUSINESS_OWNER", StringComparison.OrdinalIgnoreCase);
                })));
        return services;
    }
}
