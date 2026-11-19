#:property RestoreSources=.
#:property UseAppHost=false
#:property PublishAot=false
#:property NuGetAudit=false
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

try
{
    Console.InputEncoding = new UTF8Encoding(false);
    using var request = JsonDocument.Parse(Console.In.ReadToEnd());
    var input = request.RootElement;
    string Get(string name) => input.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    using var rsa = RSA.Create();
    if (Get("Operation") == "public")
    {
        byte[] Decode(string text) => Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/').PadRight((text.Length + 3) / 4 * 4, '='));
        rsa.ImportParameters(new RSAParameters { Modulus = Decode(Get("Modulus")), Exponent = Decode(Get("Exponent")) });
    }
    else
    {
        rsa.ImportFromPem(Get("Pem"));
        if (rsa.KeySize < 2048) throw new CryptographicException();
        _ = rsa.ExportParameters(true);
    }
    var fingerprint = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
    if (Get("Operation") == "export")
    {
        if (fingerprint != Get("ExpectedFingerprint")) throw new CryptographicException();
        var bytes = Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem());
        try
        {
            using var output = new FileStream(Get("OutputPath"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    Console.Write(JsonSerializer.Serialize(new { Fingerprint = fingerprint }));
}
catch
{
    // Never print exception messages: malformed input may contain key material.
    Console.Error.Write("Signing crypto operation failed.");
    Environment.ExitCode = 2;
}
