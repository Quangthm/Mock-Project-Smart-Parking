using System.Collections.Concurrent;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Persistence.Repositories;

// Single-process demo adapter. Restarting the API invalidates every session.
public sealed class InMemoryAuthSessionStore(TimeProvider clock) : IAuthSessionStore
{
    private sealed record Session(Guid UserId, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Session> sessions = new();

    public void Add(string sessionId, Guid userId, DateTimeOffset expiresAt)
    {
        foreach (var entry in sessions)
            if (entry.Value.ExpiresAt <= clock.GetUtcNow())
                sessions.TryRemove(entry.Key, out _);
        sessions[sessionId] = new Session(userId, expiresAt);
    }

    public bool IsActive(string sessionId, Guid userId)
    {
        if (!sessions.TryGetValue(sessionId, out var session)) return false;
        if (session.ExpiresAt <= clock.GetUtcNow())
        {
            sessions.TryRemove(sessionId, out _);
            return false;
        }
        return session.UserId == userId;
    }

    public void Revoke(string sessionId) => sessions.TryRemove(sessionId, out _);
}
