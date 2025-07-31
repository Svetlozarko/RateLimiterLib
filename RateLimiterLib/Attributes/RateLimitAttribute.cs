using System;

namespace RateLimiterLib
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RateLimitAttribute : Attribute
    {
        public int Limit { get; }
        public int Seconds { get; }

        public RateLimitAttribute(int limit, int seconds)
        {
            Limit = limit;
            Seconds = seconds;
        }
    }
}