using System.Security.Cryptography;
using System.Text;

namespace SmartParking.IoTService.Application.Assistant.Management;

// Bounded, process-local metadata only. No plaintext/protected credentials, arbitrary URLs or provider cursors.
public sealed class ModelDiscoveryCache(TimeProvider clock)
{
    private sealed record Entry(string Binding, ProviderModel[] Models, DateTimeOffset At, int Offset = 0);
    private readonly object gate = new();
    private readonly byte[] salt = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<string, Entry> saved = [];
    private readonly Dictionary<string, Entry> cursors = [];
    public string Bind(string value) => Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(value)));
    private void Prune()
    {
        var cutoff = clock.GetUtcNow().AddMinutes(-5);
        foreach (var key in saved.Where(x => x.Value.At <= cutoff).Select(x => x.Key).ToArray()) saved.Remove(key);
        foreach (var key in cursors.Where(x => x.Value.At <= cutoff).Select(x => x.Key).ToArray()) cursors.Remove(key);
    }
    public ModelPage? Get(string binding)
    {
        lock (gate) { Prune(); return saved.TryGetValue(binding, out var entry) ? Slice(entry) : null; }
    }
    public ModelPage Page(string binding, string? cursor, ProviderModel[]? models, bool cache)
    {
        lock (gate)
        {
            Prune();
            Entry entry;
            if (cursor is not null)
            {
                if (!cursors.TryGetValue(cursor, out var found) || found.Binding != binding)
                    throw new ProviderDiagnosticsException(409, "CURSOR_CONFLICT");
                entry = found;
            }
            else
            {
                entry = new(binding, models!, clock.GetUtcNow());
                if (cache) { if (saved.Count >= 128) saved.Remove(saved.Keys.First()); saved[binding] = entry; }
            }
            return Slice(entry);
        }
    }
    private ModelPage Slice(Entry entry)
    {
        var items = entry.Models.Skip(entry.Offset).Take(100).ToArray();
        string? next = null;
        if (entry.Offset + items.Length < entry.Models.Length)
        {
            if (cursors.Count >= 512) cursors.Remove(cursors.Keys.First());
            next = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            cursors[next] = entry with { Offset = entry.Offset + items.Length };
        }
        return new(items, next, entry.At);
    }
}
