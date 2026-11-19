using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.IoTService.API.Management;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Tests;

public sealed class CertificateKeyringCompositionTests
{
    [Fact]
    public async Task ExplicitCertificateReadsExistingCredentialThroughRealManagementComposition()
    {
        var directory = Path.Combine(Path.GetTempPath(), "assistant-cert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=synthetic-assistant", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificatePath = Path.Combine(directory, "synthetic.pfx");
        const string password = "synthetic-test-only";
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password));
        try
        {
            var version = Guid.NewGuid();
            string encrypted;
            var source = new ServiceCollection();
            source.AddDataProtection().SetApplicationName("SmartPark.IoT.Assistant")
                .PersistKeysToFileSystem(new DirectoryInfo(directory)).ProtectKeysWithCertificate(certificate);
            using (var services = source.BuildServiceProvider())
                encrypted = services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("SmartPark.Assistant.Admin.Key.v1", version.ToString("D")).Protect("synthetic-credential");

            var builder = WebApplication.CreateBuilder([]);
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Assistant:Jwt:Issuer"] = "synthetic", ["Assistant:Jwt:Audience"] = "synthetic",
                ["Assistant:Jwt:PublicKeyPem"] = rsa.ExportRSAPublicKeyPem(),
                ["Assistant:Management:KeyDirectory"] = directory,
                ["Assistant:Management:KeyEncryptionCertificatePath"] = certificatePath,
                ["Assistant:Management:KeyEncryptionCertificatePassword"] = password,
                ["Assistant:Guest:KnowledgeDirectory"] = directory,
                ["ConnectionStrings:IoT"] = "Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Timeout=1"
            });
            builder.AddManagement(publicGuest: true);
            await using var app = builder.Build();
            Assert.NotNull(app.Services.GetRequiredService<ManagedDemoGenerator>());
            var protector = app.Services.GetRequiredService<IAssistantSecretProtector>();
            Assert.Equal("synthetic-credential", protector.Unprotect(version, encrypted));
            Assert.Equal("synthetic-new", protector.Unprotect(version, protector.Protect(version, "synthetic-new")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
