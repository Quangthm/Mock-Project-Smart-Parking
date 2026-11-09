using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using UserService.Application.Common.Models.JwT;

namespace UserService.Infrastructure.Services;

public sealed class JwtKeyProvider : IDisposable
{
    private readonly RSA signingRsa;
    public RsaSecurityKey SigningKey { get; }
    public RsaSecurityKey ValidationKey { get; }
    public string KeyId => SigningKey.KeyId;
    public string PublicModulus { get; }
    public string PublicExponent { get; }
    public bool IsEphemeral { get; }

    public JwtKeyProvider(JwtOptions options, bool allowEphemeralKey = false)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required.");
        var configuredId = string.IsNullOrWhiteSpace(options.KeyId) ? string.Empty : options.KeyId;
        if (configuredId.Length > 128 || configuredId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new InvalidOperationException("Jwt:KeyId must contain at most 128 ASCII letters, digits, '.', '_' or '-'.");
        ValidateJwksPath(options.JwksPath);

        var hasPem = !string.IsNullOrWhiteSpace(options.PrivateKeyPem);
        var hasBase64 = !string.IsNullOrWhiteSpace(options.PrivateKeyPemBase64);
        if (!hasPem && !hasBase64 && (!allowEphemeralKey || configuredId.Length != 0))
            throw new InvalidOperationException("Configure Jwt:PrivateKeyPemBase64 or Jwt:PrivateKeyPem outside source control. A configured Jwt:KeyId requires a stable private key.");

        signingRsa = RSA.Create();
        try
        {
            IsEphemeral = !hasPem && !hasBase64;
            if (IsEphemeral)
                signingRsa.KeySize = 3072;
            else
            {
                var pem = hasBase64
                    ? new UTF8Encoding(false, true).GetString(Convert.FromBase64String(options.PrivateKeyPemBase64))
                    : options.PrivateKeyPem;
                signingRsa.ImportFromPem(pem);
                RequirePrivateKey(signingRsa);
                if (hasPem && hasBase64)
                {
                    using var legacy = RSA.Create();
                    legacy.ImportFromPem(options.PrivateKeyPem);
                    RequirePrivateKey(legacy);
                    if (!legacy.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(signingRsa.ExportSubjectPublicKeyInfo()))
                        throw new InvalidOperationException("Jwt:PrivateKeyPem and Jwt:PrivateKeyPemBase64 must identify the same RSA private key.");
                }
            }
            var publicParameters = signingRsa.ExportParameters(false);
            ValidationKey = new RsaSecurityKey(publicParameters);
            // A legacy key without an explicit ID still has a stable, key-bound RFC 7638 identifier.
            var keyId = configuredId.Length == 0
                ? Base64UrlEncoder.Encode(ValidationKey.ComputeJwkThumbprint()) : configuredId;
            SigningKey = new RsaSecurityKey(signingRsa) { KeyId = keyId };
            ValidationKey.KeyId = keyId;
            PublicModulus = Base64UrlEncoder.Encode(publicParameters.Modulus!);
            PublicExponent = Base64UrlEncoder.Encode(publicParameters.Exponent!);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            signingRsa.Dispose();
            // Do not retain an inner exception: import/decoding errors must not echo secret input in startup logs.
            throw new InvalidOperationException("JWT signing configuration must contain an unencrypted RSA private key of at least 2048 bits, encoded as PEM or Base64 UTF-8 PEM.");
        }
        catch
        {
            signingRsa.Dispose();
            throw;
        }
    }

    private static void RequirePrivateKey(RSA rsa)
    {
        if (rsa.KeySize < 2048)
            throw new CryptographicException();
        // Signing proves private-key availability without exporting private parameters.
        _ = rsa.SignData("SmartPark signing-key validation"u8, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static void ValidateJwksPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.StartsWith("//")
            || path.Length > 256 || path.EndsWith('/') || path.Contains("..")
            || path.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '/' and not '.' and not '-' and not '_'))
            throw new InvalidOperationException("Jwt:JwksPath must be an absolute local literal path without query, fragment, route parameters or traversal.");
    }

    public void Dispose() => signingRsa.Dispose();
}
