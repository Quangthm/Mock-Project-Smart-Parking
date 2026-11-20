#:property UseAppHost=false
#:property PublishAot=false
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

// Invoked by setup-assistant-docker.ps1 -Initialize. Never place secret inputs in arguments.
try
{
    Console.InputEncoding = new UTF8Encoding(false);
    var input = JsonSerializer.Deserialize<Input>(Console.In.ReadToEnd())
        ?? throw new InvalidOperationException();
    using var jwt = RSA.Create(3072);
    if (input.UseExistingSigningKey)
    {
        string? publicKey = null;
        if (!string.IsNullOrEmpty(input.Pem))
        {
            jwt.ImportFromPem(input.Pem);
            _ = jwt.ExportParameters(true);
            publicKey = jwt.ExportSubjectPublicKeyInfoPem();
        }
        if (!string.IsNullOrEmpty(input.Base64))
        {
            jwt.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(input.Base64)));
            _ = jwt.ExportParameters(true);
            if (publicKey is not null && publicKey != jwt.ExportSubjectPublicKeyInfoPem())
                throw new InvalidOperationException();
        }
        else if (publicKey is null) throw new InvalidOperationException();
    }

    // Validate reused keys before creating any material or updating .env.
    Directory.CreateDirectory(input.Directory);
    Directory.CreateDirectory(Path.Combine(input.Directory, "keyring"));
    using var protection = RSA.Create(3072);
    using var caKey = RSA.Create(3072);
    using var serverKey = RSA.Create(3072);
    var now = DateTimeOffset.UtcNow;
    var protectionPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var serverPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    void WriteText(string name, string value) => File.WriteAllText(Path.Combine(input.Directory, name), value);
    void WriteBytes(string name, byte[] value) => File.WriteAllBytes(Path.Combine(input.Directory, name), value);
    if (!input.UseExistingSigningKey) WriteText("user-private.pem", jwt.ExportPkcs8PrivateKeyPem());
    WriteText("user-public.pem", jwt.ExportSubjectPublicKeyInfoPem());
    var request = new CertificateRequest("CN=SmartPark local Assistant keyring", protection,
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using var protectionCert = request.CreateSelfSigned(now.AddMinutes(-5), now.AddYears(3));
    WriteBytes("assistant.pfx", protectionCert.Export(X509ContentType.Pfx, protectionPassword));
    request = new CertificateRequest("CN=SmartPark local User CA", caKey,
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
    using var ca = request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(100));
    WriteText("user-ca.crt", ca.ExportCertificatePem());
    request = new CertificateRequest("CN=user-service", serverKey,
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var san = new SubjectAlternativeNameBuilder();
    san.AddDnsName("user-service");
    request.CertificateExtensions.Add(san.Build());
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    var usage = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") };
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usage, true));
    using var leaf = request.Create(ca, now.AddMinutes(-1), now.AddDays(90), RandomNumberGenerator.GetBytes(16));
    using var serverCert = leaf.CopyWithPrivateKey(serverKey);
    WriteBytes("user-service.pfx", serverCert.Export(X509ContentType.Pfx, serverPassword));
    Console.Write(JsonSerializer.Serialize(new { ProtectionPassword = protectionPassword, ServerPassword = serverPassword }));
    return 0;
}
catch
{
    // Do not echo exception details: PEM, paths and passwords are private local inputs.
    Console.Error.WriteLine("Assistant cryptographic material generation failed.");
    return 1;
}

sealed record Input(string Directory, bool UseExistingSigningKey, string? Pem, string? Base64);
