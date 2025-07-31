using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RateLimiterLib
{
    public class RateLimiterSlidingWindow : IRateLimiter
    {
        private readonly int _limit;
        private readonly TimeSpan _window;
        private readonly ConcurrentDictionary<string, SlidingEntry> _entries = new();

        private class SlidingEntry
        {
            private readonly Queue<DateTime> _timestamps = new();
            private readonly object _lock = new();

            public bool TryAllow(int limit, TimeSpan window)
            {
                var now = DateTime.UtcNow;

                lock (_lock)
                {
                    // Remove expired timestamps
                    while (_timestamps.Count > 0 && now - _timestamps.Peek() > window)
                    {
                        _timestamps.Dequeue();
                    }

                    if (_timestamps.Count < limit)
                    {
                        _timestamps.Enqueue(now);
                        return true;
                    }

                    return false;
                }
            }
        }

        public RateLimiterSlidingWindow(int limit, TimeSpan window)
        {
            _limit = limit;
            _window = window;
        }

        public Task<bool> AllowRequestAsync(string key)
        {
            var entry = _entries.GetOrAdd(key, _ => new SlidingEntry());

            bool allowed = entry.TryAllow(_limit, _window);

            return Task.FromResult(allowed);
        }


    }
}