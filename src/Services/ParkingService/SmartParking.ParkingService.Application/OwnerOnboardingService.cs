using System.Text.Json;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Application;

public sealed class OwnerOnboardingService(IOwnerOnboardingStore store)
{
    public Task ProvisionAsync(Guid applicationId, OwnerOnboardingBody body, CancellationToken ct)
    {
        if (applicationId == Guid.Empty || body.TenantId != applicationId ||
            string.IsNullOrWhiteSpace(body.Name) || body.Name.Length > 255 ||
            string.IsNullOrWhiteSpace(body.Email) || body.Email.Length > 255 ||
            string.IsNullOrWhiteSpace(body.Phone) || body.Phone.Length > 20 ||
            body.Lot.ValueKind != JsonValueKind.Object ||
            !body.Lot.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(name.GetString()) || name.GetString()!.Length > 255 ||
            !body.Lot.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(address.GetString()) || address.GetString()!.Length > 2000)
            throw new StructureException("INVALID_ONBOARDING", "Invalid onboarding site.");
        return store.ProvisionAsync(applicationId, body, ct);
    }
}
