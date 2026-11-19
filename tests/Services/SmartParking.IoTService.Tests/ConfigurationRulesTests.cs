using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Tests;

public sealed class ConfigurationRulesTests
{
    [Fact]
    public void UpgradePreservesIdentityVersionTogglePriorityAndRevokedData()
    {
        var key = new ManagedKey(Guid.NewGuid(), "gemini", "Original", false, 7, Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var state = ManagementState.Empty with { Revision = 13, Enabled = true, Keys = [key] };
        var migrated = ManagementRules.Upgrade(state);
        Assert.Equal(key with { Model = state.Providers[0].Model }, Assert.Single(migrated.Keys));
        Assert.Equal(state.Revision, migrated.Revision);
        Assert.Equal(state.Enabled, migrated.Enabled);
        Assert.Equal(migrated.Keys, ManagementRules.Upgrade(migrated).Keys);
    }
    [Fact]
    public void LegacyEditsPreservePerConfigurationModelAndRejectProviderModelOverwrite()
    {
        var key = Key("gemini", 1) with { Model = "model-per-key" };
        var state = ManagementState.Empty with { Keys = [key] };
        var write = Apply(state, new(Guid.NewGuid(), 0, "save-key", KeyId: key.Id, Provider: "gemini", Label: "Renamed", Enabled: false, Priority: 1));
        Assert.Equal("model-per-key", Assert.Single(write.State.Keys).Model);
        Assert.Null(write.Credential);
        Assert.Throws<ClientUpgradeRequiredException>(() => Apply(state, new(Guid.NewGuid(), 0, "save-config",
            Enabled: false, MaintenanceMessage: "Maintenance", Providers: state.Providers.Select(p => p with { Model = "overwritten" }).ToArray())));
    }
    [Fact]
    public void AddIsDisabledSetMovesWholeListAndRotatePreservesModelAndIdentity()
    {
        var state = ManagementState.Empty with { Keys = [Key("gemini", 1), Key("gemini", 2)] };
        var added = Apply(state, new(Guid.NewGuid(), 0, "add-configuration", Provider: "gemini", Label: "New", Model: "gemini-3.8-flash", Secret: "synthetic-secret", Priority: 1));
        var key = added.State.Keys.Single(k => k.Label == "New");
        Assert.False(key.Enabled);
        Assert.Equal([1, 2, 3], added.State.Keys.OrderBy(k => k.Priority).Select(k => k.Priority));
        var changed = Apply(added.State, new(Guid.NewGuid(), 1, "set-configuration", KeyId: key.Id, Label: "Edited", Model: "gemini-3.6-flash", Enabled: true, Priority: 3));
        Assert.Equal(key.Id, changed.State.Keys.Single(k => k.Priority == 3).Id);
        var rotated = Apply(changed.State, new(Guid.NewGuid(), 2, "rotate-key", KeyId: key.Id, Secret: "replacement-secret"));
        var actual = rotated.State.Keys.Single(k => k.Id == key.Id);
        Assert.Equal("gemini-3.6-flash", actual.Model);
        Assert.True(actual.Enabled);
        Assert.Equal(key.CredentialVersion, rotated.RetireVersion);
        Assert.NotEqual(key.CredentialVersion, actual.CredentialVersion);
    }
    [Fact]
    public void ReorderRequiresEveryActiveScopedIdExactlyOnce()
    {
        var a = Key("gemini", 1); var b = Key("gemini", 2); var other = Key("openai", 1);
        var state = ManagementState.Empty with { Keys = [a, b, other] };
        foreach (var ids in new[] { new[] { a.Id }, new[] { a.Id, a.Id }, new[] { a.Id, other.Id } })
            Assert.Throws<ManagementValidationException>(() => Apply(state, new(Guid.NewGuid(), 0, "reorder-configurations", Provider: "gemini", ConfigurationIds: ids)));
        var write = Apply(state, new(Guid.NewGuid(), 0, "reorder-configurations", Provider: "gemini", ConfigurationIds: [b.Id, a.Id]));
        Assert.Equal(1, write.State.Keys.Single(k => k.Id == b.Id).Priority);
        Assert.Equal(other, write.State.Keys.Single(k => k.Id == other.Id));
        Assert.Equal(1, write.State.Revision);
    }
    [Theory]
    [InlineData("https://evil.example/model")]
    [InlineData("../model")]
    [InlineData("")]
    public void ArbitraryModelPathsAreRejected(string model) => Assert.Throws<ManagementValidationException>(() =>
        Apply(ManagementState.Empty, new(Guid.NewGuid(), 0, "add-configuration", Provider: "gemini", Label: "New", Model: model, Secret: "synthetic-secret", Priority: 1)));
    [Fact]
    public void LegacyRetryKeepsIdenticalReceiptBytes()
    {
        var id = Guid.NewGuid();
        var input = new AdminMutation(id, 7, "save-key", Label: "Primary", Secret: "synthetic-secret");
        var historical = new { MutationId = id, ExpectedRevision = 7, Action = "save-key", Providers = (object?)null,
            Enabled = (bool?)null, MaintenanceMessage = (string?)null, KeyId = (Guid?)null, Provider = (string?)null,
            Label = "Primary", Secret = "synthetic-secret", Priority = (int?)null };
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(historical), System.Text.Json.JsonSerializer.Serialize(input));
        Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<AdminMutation>("{}"));
    }

    private static ManagedKey Key(string provider, int priority) => new(Guid.NewGuid(), provider, "Key", true, priority, Guid.NewGuid(), DateTimeOffset.UtcNow, null, "model-original");
    private static ManagementWrite Apply(ManagementState state, AdminMutation input) => ManagementRules.Apply(state, input, new Protector(), DateTimeOffset.UtcNow);
    private sealed class Protector : IAssistantSecretProtector
    {
        public bool IsConfigured => true;
        public string Protect(Guid version, string secret) => "encrypted";
        public string Unprotect(Guid version, string protectedValue) => throw new NotSupportedException();
    }
}
