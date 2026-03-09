using System.Collections.Concurrent;

namespace RegistrationApi.Services;

public sealed class RateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();

    public RateLimiter(int maxRequests, TimeSpan window)
    {
        _maxRequests = maxRequests;
        _window = window;
    }

    public bool IsAllowed(string key)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - _window;

        var timestamps = _requests.GetOrAdd(key, _ => new List<DateTime>());

        lock (timestamps)
        {
            timestamps.RemoveAll(t => t < cutoff);

            if (timestamps.Count >= _maxRequests)
                return false;

            timestamps.Add(now);
            return true;
        }
    }

    public void Cleanup()
    {
        var cutoff = DateTime.UtcNow - _window;
        foreach (var kvp in _requests)
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => t < cutoff);
                if (kvp.Value.Count == 0)
                    _requests.TryRemove(kvp.Key, out _);
            }
        }
    }
}
