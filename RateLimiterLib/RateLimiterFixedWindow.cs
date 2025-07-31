using System;
using System.Collections.Concurrent;

namespace RateLimiterLib
{
    public class RateLimiterFixedWindow : IRateLimiter
    {
        private readonly int _limit;
        private readonly TimeSpan _window;
        private readonly ConcurrentDictionary<string, Entry> _entries = new();

        private class Entry
        {
            public int Count;
            public DateTime WindowStart;
            private readonly object _lock = new();

            public Entry(DateTime windowStart)
            {
                WindowStart = windowStart;
                Count = 1;
            }

            public bool TryIncrement(int limit, TimeSpan window)
            {
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    if (now - WindowStart > window)
                    {
                        // Reset window
                        WindowStart = now;
                        Count = 1;
                        return true;
                    }

                    if (Count < limit)
                    {
                        Count++;
                        return true;
                    }

                    return false;
                }
            }
        }

        public RateLimiterFixedWindow(int limit, TimeSpan window)
        {
            _limit = limit;
            _window = window;
        }

        public bool AllowRequest(string key)
        {
            var now = DateTime.UtcNow;
            var entry = _entries.GetOrAdd(key, _ => new Entry(now));
            return entry.TryIncrement(_limit, _window);
        }
    }
}   