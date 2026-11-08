using System.Text.Json;

namespace SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

public sealed record OwnerOnboardingBody(Guid TenantId, string Name, string Email, string Phone, JsonElement Lot);

public interface IOwnerOnboardingStore
{
    Task ProvisionAsync(Guid applicationId, OwnerOnboardingBody body, CancellationToken ct);
}
