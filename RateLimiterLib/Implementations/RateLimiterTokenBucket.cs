using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RateLimiterLib
{
    public class RateLimiterTokenBucket : IRateLimiter
    {
        private readonly int _capacity;           // max tokens in bucket
        private readonly double _refillTokensPerSecond; // refill rate (tokens per second)
        private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new();

        private class TokenBucket
        {
            private readonly int _capacity;
            private readonly double _refillRate;
            private double _tokens;
            private DateTime _lastRefillTimestamp;
            private readonly object _lock = new();

            public TokenBucket(int capacity, double refillRate)
            {
                _capacity = capacity;
                _refillRate = refillRate;
                _tokens = capacity;
                _lastRefillTimestamp = DateTime.UtcNow;
            }

            public bool TryConsume()
            {
                lock (_lock)
                {
                    Refill();

                    if (_tokens >= 1)
                    {
                        _tokens -= 1;
                        return true;
                    }

                    return false;
                }
            }

            private void Refill()
            {
                var now = DateTime.UtcNow;
                var secondsPassed = (now - _lastRefillTimestamp).TotalSeconds;
                var tokensToAdd = secondsPassed * _refillRate;
                if (tokensToAdd > 0)
                {
                    _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
                    _lastRefillTimestamp = now;
                }
            }
        }

        public RateLimiterTokenBucket(int capacity, TimeSpan refillInterval)
        {
            _capacity = capacity;
            // Calculate refill rate tokens per second
            _refillTokensPerSecond = capacity / refillInterval.TotalSeconds;
        }

        public Task<bool> AllowRequestAsync(string key)
        {
            var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket(_capacity, _refillTokensPerSecond));
            bool allowed = bucket.TryConsume();
            return Task.FromResult(allowed);
        }
    }
}
