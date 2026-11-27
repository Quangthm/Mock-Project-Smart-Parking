using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SmartParking.OperationService.Application.Discovery;

namespace SmartParking.OperationService.API;

public sealed class DiscoveryCursors(IDataProtectionProvider provider) : IDiscoveryCursors
{
    private readonly IDataProtector protector = provider.CreateProtector("Operations.PublicDiscovery.Cursor.v1");
    public string Encode(DiscoveryCursor value) => protector.Protect(JsonSerializer.Serialize(value));
    public DiscoveryCursor Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048) throw new DiscoveryFailure(400, "INVALID_CURSOR");
        try
        {
            return JsonSerializer.Deserialize<DiscoveryCursor>(protector.Unprotect(value))
                ?? throw new DiscoveryFailure(400, "INVALID_CURSOR");
        }
        catch (Exception e) when (e is CryptographicException or FormatException or JsonException)
        {
            throw new DiscoveryFailure(400, "INVALID_CURSOR");
        }
    }
}
