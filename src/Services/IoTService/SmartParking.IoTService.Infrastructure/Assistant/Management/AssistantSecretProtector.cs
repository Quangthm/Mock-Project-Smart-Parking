using Microsoft.AspNetCore.DataProtection;
using SmartParking.IoTService.Application.Assistant.Management;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Infrastructure.Assistant.Knowledge;
using System.Security.Cryptography;
namespace SmartParking.IoTService.Infrastructure.Assistant.Management;
public sealed class AssistantSecretProtector(IDataProtectionProvider provider) : IAssistantSecretProtector
{
    public bool IsConfigured => true;
    public string Protect(Guid version,string secret) => provider.CreateProtector("SmartPark.Assistant.Admin.Key.v1",version.ToString("D")).Protect(secret);
    public string Unprotect(Guid version,string value)
    { try { return provider.CreateProtector("SmartPark.Assistant.Admin.Key.v1",version.ToString("D")).Unprotect(value); }
      catch(CryptographicException) { throw new ManagementUnavailableException(); } }
}
public sealed class ManagedAnswerProvider(string knowledgeDirectory) : IManagedAnswerProvider, IDisposable
{
    private readonly SemaphoreSlim slots = new(2,2);
    public async Task<GroundedReply> GenerateAsync(string provider,string model,string secret,string message,CancellationToken ct)
    {
        if (!await slots.WaitAsync(0,ct)) throw new DemoGroundedUnavailableException(DemoGroundedFailure.Busy);
        try { using var generator = new DemoGroundedGenerator(new(knowledgeDirectory,provider,model,secret));
            return await generator.GenerateAsync(message,ct); }
        finally { slots.Release(); }
    }
    public Task<GroundedReply> KnowledgeAsync(string message, CancellationToken ct) =>
        Task.FromResult(DemoKnowledgeGuidance.Read(knowledgeDirectory, message, ct));
    public void Dispose() => slots.Dispose();
}
