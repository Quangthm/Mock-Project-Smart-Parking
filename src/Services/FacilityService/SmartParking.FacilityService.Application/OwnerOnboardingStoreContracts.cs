using System.Text.Json;
namespace SmartParking.FacilityService.Application;

public sealed record OwnerOnboardingBody(Guid TenantId, string Name, string Email, string Phone, JsonElement Lot);

// Called only over the authenticated service channel after UserService commits approval intent.

public interface IOwnerOnboardingStore
{
    Task ProvisionAsync(Guid applicationId, OwnerOnboardingBody body, CancellationToken ct);
}
