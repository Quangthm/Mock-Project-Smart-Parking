namespace UserService.Application.Common.Models.JwT;

public sealed class JwtOptions
{
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string PrivateKeyPemBase64 { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string JwksPath { get; set; } = "/.well-known/jwks.json";
}
