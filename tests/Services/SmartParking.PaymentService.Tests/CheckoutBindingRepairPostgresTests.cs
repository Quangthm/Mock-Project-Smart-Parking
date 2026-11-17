using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Persistence.Repositories;
using Xunit;

namespace SmartParking.PaymentService.Tests;

public partial class PaymentIntegrationPostgresConcurrencyTests
{
    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task BindingUsesTrustedOrderIdentity_RejectsOrphans_AndPreventsOrderDeletion()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();
        const long code = 741852969;
        await SeedCheckoutAsync(fixture, code);
        await SeedCheckoutAsync(fixture, code + 2);
        await using var db = fixture.Open();
        var order = await db.PaymentOrders.SingleAsync(row => row.OrderCode == code.ToString());
        order.OrderCode = "non-numeric-local-reference";
        await db.SaveChangesAsync();
        var bindings = new CheckoutSigningBindings(new CheckoutBindingRepository(db), new EphemeralDataProtectionProvider());
        var configuration = new PaymentConfigurationSnapshot(2, "LIVE", string.Empty, null, null, "synthetic-binding-key");
        Assert.True(await bindings.BindAsync(code, configuration, default,
            new("CHECKOUT", PaymentOrderId: order.Id)));
        Assert.Equal(order.Id, (await db.Set<CheckoutSigningBinding>().SingleAsync()).PaymentOrderId);
        var differentOrder = await db.PaymentOrders.SingleAsync(row => row.OrderCode == (code + 2).ToString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => bindings.BindAsync(code, configuration, default,
            new("CHECKOUT", PaymentOrderId: differentOrder.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bindings.BindAsync(code + 1, configuration, default,
            new("CHECKOUT", PaymentOrderId: Guid.NewGuid())));
        await db.PaymentTransactions.Where(row => row.PaymentOrderId == order.Id).ExecuteDeleteAsync();
        var foreignKey = await Assert.ThrowsAsync<PostgresException>(() => db.PaymentOrders.Where(row => row.Id == order.Id).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, foreignKey.SqlState);
    }

    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task LegacyPolicyBinding_RemainsVerifiable_MigratesWithoutTariffWrites_AndSurvivesLegacyRemoval()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();
        const long code = 741852970;
        await SeedCheckoutAsync(fixture, code);
        var protection = new EphemeralDataProtectionProvider();
        var memory = new CheckoutBindingFixture();
        var old = new CheckoutSigningBindings(memory, protection);
        var configuration = new PaymentConfigurationSnapshot(5, "SANDBOX", string.Empty, null, null, "synthetic-issued-key");
        Assert.True(await old.BindAsync(code, configuration, default));
        var protectedValue = await memory.ReadAsync(code, default);
        var id = Guid.ParseExact($"cb04973f52fe4d7b{code:x16}", "N");
        await using var db = fixture.Open();
        db.PolicyRevisions.Add(new PolicyRevision { Id = id, PolicyId = id, ScopeType = "GLOBAL",
            PolicyValuesJson = JsonSerializer.Serialize(protectedValue), CreatedBy = Guid.Empty });
        await db.SaveChangesAsync();
        var policyCount = await db.PolicyRevisions.CountAsync();
        var bindings = new CheckoutSigningBindings(new CheckoutBindingRepository(db), protection);

        Assert.Equal(configuration.ChecksumKey, await bindings.ReadChecksumKeyAsync(code, default));
        Assert.True(await bindings.MigrateLegacyAsync(code, "owner-audit/legacy-1", false, default));
        Assert.Empty(await db.Set<CheckoutSigningBinding>().ToListAsync());
        Assert.False(await bindings.BindAsync(code, configuration with { ChecksumKey = "synthetic-wrong-key" }, default));
        Assert.True(await bindings.MigrateLegacyAsync(code, "owner-audit/legacy-1", true, default));
        Assert.Equal(policyCount, await db.PolicyRevisions.CountAsync());
        var row = await db.Set<CheckoutSigningBinding>().SingleAsync();
        Assert.Equal("LEGACY_POLICY", row.Source);
        Assert.Equal("owner-audit/legacy-1", row.EvidenceReference);
        Assert.DoesNotContain(configuration.ChecksumKey!, row.ProtectedBinding);

        // Delete only the generated legacy fixture row, never any application/pricing database.
        await db.PolicyRevisions.Where(policy => policy.Id == id).ExecuteDeleteAsync();
        await using var restarted = fixture.Open();
        Assert.Equal(configuration.ChecksumKey, await new CheckoutSigningBindings(new CheckoutBindingRepository(restarted), protection)
            .ReadChecksumKeyAsync(code, default));

        // The additive production upgrade is idempotent on the isolated database.
        var upgrade = await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "scripts/database/upgrades/20261117-payment-checkout-signing-bindings.sql"));
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = upgrade;
        await command.ExecuteNonQueryAsync();
        await command.ExecuteNonQueryAsync();
        Assert.Equal(1, await db.Set<CheckoutSigningBinding>().CountAsync());
    }

    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task ConcurrentDifferentKeys_OnlyOneImmutableBindingWins_AndPricingHistoryDoesNotGrow()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();
        const long code = 741852971;
        await SeedCheckoutAsync(fixture, code);
        var protection = new EphemeralDataProtectionProvider();
        await using var observation = fixture.Open();
        var policyCount = await observation.PolicyRevisions.CountAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Bind(string key)
        {
            await using var db = fixture.Open();
            var binding = new CheckoutSigningBindings(new CheckoutBindingRepository(db), protection);
            await start.Task;
            return await binding.BindAsync(code, new(3, "LIVE", string.Empty, null, null, key), default);
        }
        var first = Bind("synthetic-first-key");
        var second = Bind("synthetic-second-key");
        start.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, result => result);
        Assert.Equal(1, await observation.Set<CheckoutSigningBinding>().CountAsync());
        Assert.Equal(policyCount, await observation.PolicyRevisions.CountAsync());
        var reader = new CheckoutSigningBindings(new CheckoutBindingRepository(observation), protection);
        Assert.Equal(results[0] ? "synthetic-first-key" : "synthetic-second-key", await reader.ReadChecksumKeyAsync(code, default));
    }

    [PaymentPostgresFact, Trait("Category", "Postgres")]
    public async Task OfflineRepair_DryRunApplyReplayConflictAndBatchPreflight_ThenWebhookSurvivesRestart()
    {
        await using var fixture = await PaymentTempDatabase.CreateAsync();
        const long code = 741852972;
        await SeedCheckoutAsync(fixture, code);
        await SeedCheckoutAsync(fixture, code + 1);
        await SeedCheckoutAsync(fixture, code + 2);
        var directory = Directory.CreateTempSubdirectory("smartpark-binding-repair-");
        var keys = directory.CreateSubdirectory("keys");
        var manifest = Path.Combine(directory.FullName, "private-manifest.json");
        const string oldKey = "synthetic-predeployment-key";
        LegacyCheckoutBindingInput Input(long orderCode, string key, string? link = null) => new()
        {
            OrderCode = orderCode, ProviderLinkId = link ?? "legacy-link-" + orderCode, Mode = "SANDBOX", Version = 4,
            ChecksumKey = key, EvidenceReference = "owner-verified-provider-export/entry-1"
        };
        try
        {
            using (var provider = KeyProvider(keys))
                provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("warmup").Protect("warmup");
            await using var db = fixture.Open();
            var options = Options.Create(new PayOsOptions { Mode = "LIVE", LiveClientId = "synthetic-client",
                LiveApiKey = "synthetic-api", LiveChecksumKey = "synthetic-current-key" });
            var data = new Dictionary<string, object?> { ["orderCode"] = code, ["amount"] = 25000,
                ["paymentLinkId"] = "legacy-link-" + code, ["reference"] = "synthetic-reference" };
            var signature = PayOsSignatureService.CreateSignatureFromDictionary(data, oldKey);
            var webhook = JsonSerializer.Serialize(new { code = "00", data, signature });
            using (var provider = KeyProvider(keys))
            {
                var adapter = new PayOsAdapter(new HttpClient(), options, NullLogger<PayOsAdapter>.Instance,
                    new PaymentIntegrationSettingsStore(options), new CheckoutSigningBindings(new CheckoutBindingRepository(db),
                        provider.GetRequiredService<IDataProtectionProvider>()));
                Assert.False((await adapter.VerifyAndParseWebhookAsync(webhook, signature)).IsValidSignature);
            }
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new[] { Input(code, oldKey) }));
            var inventory = await RepairAsync(fixture.ConnectionString, keys.FullName, "--inventory");
            Assert.Equal(0, inventory.ExitCode);
            Assert.Contains("MISSING_BINDING", inventory.Output);
            var dry = await RepairAsync(fixture.ConnectionString, keys.FullName, "--manifest", manifest);
            Assert.Equal(0, dry.ExitCode);
            Assert.Contains("READY", dry.Output);
            Assert.Equal(0, await db.Set<CheckoutSigningBinding>().CountAsync());
            var applied = await RepairAsync(fixture.ConnectionString, keys.FullName, "--manifest", manifest, "--apply");
            Assert.Equal(0, applied.ExitCode);
            Assert.Contains("APPLIED", applied.Output);
            Assert.DoesNotContain(oldKey, inventory.Output + dry.Output + applied.Output);
            var stored = await db.Set<CheckoutSigningBinding>().AsNoTracking().SingleAsync();
            Assert.Equal("LEGACY_IMPORT", stored.Source);
            Assert.DoesNotContain(oldKey, stored.ProtectedBinding);
            Assert.Equal(0, (await RepairAsync(fixture.ConnectionString, keys.FullName, "--manifest", manifest, "--apply")).ExitCode);
            Assert.Equal(stored.ProtectedBinding, (await db.Set<CheckoutSigningBinding>().AsNoTracking().SingleAsync()).ProtectedBinding);

            using (var provider = KeyProvider(keys))
            await using (var restarted = fixture.Open())
            {
                var adapter = new PayOsAdapter(new HttpClient(), options, NullLogger<PayOsAdapter>.Instance,
                    new PaymentIntegrationSettingsStore(options), new CheckoutSigningBindings(new CheckoutBindingRepository(restarted),
                        provider.GetRequiredService<IDataProtectionProvider>()));
                Assert.True((await adapter.VerifyAndParseWebhookAsync(webhook, signature)).IsValidSignature);
                Assert.False((await adapter.VerifyAndParseWebhookAsync(webhook, "tampered-signature")).IsValidSignature);
            }
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new[] { Input(code, "synthetic-conflicting-key") }));
            var conflict = await RepairAsync(fixture.ConnectionString, keys.FullName, "--manifest", manifest, "--apply");
            Assert.Equal(2, conflict.ExitCode);
            Assert.Contains("CONFLICT", conflict.Output);
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new[] { Input(code + 1, oldKey), Input(code + 2, oldKey, "wrong-link") }));
            var invalidBatch = await RepairAsync(fixture.ConnectionString, keys.FullName, "--manifest", manifest, "--apply");
            Assert.Equal(2, invalidBatch.ExitCode);
            Assert.Contains("CHECKOUT_EVIDENCE_MISMATCH", invalidBatch.Output);
            Assert.Equal(1, await db.Set<CheckoutSigningBinding>().CountAsync());
            await File.WriteAllTextAsync(manifest, "{\"checksumKey\":\"" + oldKey + "\"");
            var malformed = await RepairAsync(fixture.ConnectionString, keys.FullName, "--manifest", manifest);
            Assert.Equal(1, malformed.ExitCode);
            Assert.DoesNotContain(oldKey, malformed.Output);
        }
        finally { DeleteTemporaryBindingDirectory(directory); }
    }

    private static void DeleteTemporaryBindingDirectory(DirectoryInfo directory)
    {
        var path = Path.GetFullPath(directory.FullName);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(root, comparison) || !directory.Name.StartsWith("smartpark-binding-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the generated binding-test directory.");
        Directory.Delete(path, recursive: true);
    }

    private static ServiceProvider KeyProvider(DirectoryInfo keys)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("SmartParking.PaymentService").PersistKeysToFileSystem(keys);
        return services.BuildServiceProvider();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "SmartParking.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Repository root unavailable.");
    }

    private static async Task<(int ExitCode, string Output)> RepairAsync(string connection, string keys, params string[] arguments)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Path.Combine(RepositoryRoot(), "scripts/payment/SmartParking.CheckoutBindingRepair/bin", configuration,
            "net10.0/SmartParking.CheckoutBindingRepair.dll");
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = RepositoryRoot() };
        info.ArgumentList.Add(dll);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment["ConnectionStrings__Payment"] = connection;
        info.Environment["DataProtection__KeysDirectory"] = keys;
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output + await error);
    }
}
