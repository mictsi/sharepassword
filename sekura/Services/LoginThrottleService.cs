using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Sekura.Options;

namespace Sekura.Services;

public interface ILoginThrottleService
{
    /// <summary>Returns the UTC time until which sign-in is paused for the account, or null when it may proceed.</summary>
    DateTime? GetPauseExpiryUtc(string username);

    /// <summary>Records a failed sign-in attempt. Returns the pause expiry when this failure triggered a pause.</summary>
    DateTime? RecordFailure(string username);

    void RecordSuccess(string username);
}

public sealed class LoginThrottleService : ILoginThrottleService
{
    // Bounds memory when an attacker sprays random usernames; stale entries are
    // pruned first, and the tracker refuses new entries only past this hard cap.
    private const int MaxTrackedAccounts = 100_000;

    private readonly ConcurrentDictionary<string, ThrottleEntry> _entries = new();
    private readonly IApplicationTime _applicationTime;
    private readonly LoginThrottleOptions _options;

    public LoginThrottleService(IApplicationTime applicationTime, IOptions<LoginThrottleOptions> options)
    {
        _applicationTime = applicationTime;
        _options = options.Value;
    }

    public DateTime? GetPauseExpiryUtc(string username)
    {
        if (!_entries.TryGetValue(Normalize(username), out var entry))
        {
            return null;
        }

        lock (entry)
        {
            return entry.PausedUntilUtc > _applicationTime.UtcNow ? entry.PausedUntilUtc : null;
        }
    }

    public DateTime? RecordFailure(string username)
    {
        var key = Normalize(username);
        var utcNow = _applicationTime.UtcNow;

        PruneIfNeeded(utcNow);

        var entry = _entries.GetOrAdd(key, _ => new ThrottleEntry());
        lock (entry)
        {
            if (entry.PausedUntilUtc is { } pausedUntilUtc && pausedUntilUtc <= utcNow)
            {
                entry.PausedUntilUtc = null;
                entry.FailedAttempts = 0;
            }

            if (entry.LastFailureUtc is { } lastFailureUtc
                && lastFailureUtc.AddMinutes(Math.Max(1, _options.FailureWindowMinutes)) <= utcNow)
            {
                entry.FailedAttempts = 0;
            }

            entry.FailedAttempts++;
            entry.LastFailureUtc = utcNow;

            if (entry.PausedUntilUtc is null && entry.FailedAttempts >= Math.Max(1, _options.FailedAttemptLimit))
            {
                entry.PausedUntilUtc = utcNow.AddMinutes(Math.Max(1, _options.PauseMinutes));
                return entry.PausedUntilUtc;
            }

            return null;
        }
    }

    public void RecordSuccess(string username)
    {
        _entries.TryRemove(Normalize(username), out _);
    }

    private void PruneIfNeeded(DateTime utcNow)
    {
        if (_entries.Count < MaxTrackedAccounts)
        {
            return;
        }

        var staleBefore = utcNow.AddMinutes(-Math.Max(1, _options.FailureWindowMinutes));
        foreach (var pair in _entries)
        {
            lock (pair.Value)
            {
                var isPaused = pair.Value.PausedUntilUtc > utcNow;
                if (!isPaused && (pair.Value.LastFailureUtc is null || pair.Value.LastFailureUtc < staleBefore))
                {
                    _entries.TryRemove(pair.Key, out _);
                }
            }
        }
    }

    private static string Normalize(string username)
    {
        return (username ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed class ThrottleEntry
    {
        public int FailedAttempts;
        public DateTime? LastFailureUtc;
        public DateTime? PausedUntilUtc;
    }
}
