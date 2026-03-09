using System.Collections.Concurrent;

namespace RegistrationApi.Services;

public sealed class OAuthSessionStore
{
    private readonly ConcurrentDictionary<string, OAuthSession> _sessions = new();
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(5);

    public void Create(string sessionId)
    {
        _sessions[sessionId] = new OAuthSession(DateTime.UtcNow);
    }

    public void Complete(string sessionId, string username, string password)
    {
        _sessions[sessionId] = new OAuthSession(DateTime.UtcNow)
        {
            Status = "ok",
            Username = username,
            Password = password
        };
    }

    public void Fail(string sessionId, string error)
    {
        _sessions[sessionId] = new OAuthSession(DateTime.UtcNow)
        {
            Status = "error",
            Error = error
        };
    }

    public OAuthSession? Get(string sessionId, bool consume = false)
    {
        if (consume)
        {
            // Remove after reading — credentials can only be read once
            if (_sessions.TryRemove(sessionId, out var session) && session.Status != "pending")
                return session;
            // Put it back if still pending
            if (session != null)
                _sessions.TryAdd(sessionId, session);
            return session;
        }

        if (_sessions.TryGetValue(sessionId, out var s))
            return s;
        return null;
    }

    public void Cleanup()
    {
        var cutoff = DateTime.UtcNow - SessionTtl;
        foreach (var key in _sessions.Keys)
        {
            if (_sessions.TryGetValue(key, out var s) && s.CreatedAt < cutoff)
                _sessions.TryRemove(key, out _);
        }
    }
}

public sealed class OAuthSession
{
    public DateTime CreatedAt { get; }
    public string Status { get; init; } = "pending";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Error { get; init; }

    public OAuthSession(DateTime createdAt) => CreatedAt = createdAt;
}
