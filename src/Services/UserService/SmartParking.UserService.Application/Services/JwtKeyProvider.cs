using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using UserService.Application.Common.Models.JwT;

namespace UserService.Application.Services;

public sealed class JwtKeyProvider : IDisposable
{
    private readonly RSA signingRsa;
    public RsaSecurityKey SigningKey { get; }
    public RsaSecurityKey ValidationKey { get; }

    public JwtKeyProvider(JwtOptions options, bool allowEphemeralKey = false)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required.");
        signingRsa = RSA.Create(2048);
        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPem))
            signingRsa.ImportFromPem(options.PrivateKeyPem);
        else if (!allowEphemeralKey)
            throw new InvalidOperationException("Configure Jwt:PrivateKeyPem outside source control.");
        if (signingRsa.KeySize < 2048)
            throw new InvalidOperationException("The RSA signing key must be at least 2048 bits.");
        // Verify that a signing private key was supplied, not just a public key.
        signingRsa.ExportParameters(true);
        SigningKey = new RsaSecurityKey(signingRsa);
        ValidationKey = new RsaSecurityKey(signingRsa.ExportParameters(false));
    }

    public void Dispose() => signingRsa.Dispose();
}
