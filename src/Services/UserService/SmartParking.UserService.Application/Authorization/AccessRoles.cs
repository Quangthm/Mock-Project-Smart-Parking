namespace UserService.Application.Authorization;

/// <summary>Maps the fixed identity role catalogue to the existing public role names.</summary>
public static class AccessRoles
{
    public static string? Map(string? code) => code?.ToUpperInvariant() switch
    {
        "PLATFORM_ADMIN" or "ADMIN" => "admin",
        "BUSINESS_OWNER" or "OWNER" => "owner",
        "SITE_OPERATOR" or "OPERATOR" => "operator",
        "DRIVER" => "driver",
        _ => null
    };
}
