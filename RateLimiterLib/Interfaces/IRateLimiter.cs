namespace RateLimiterLib
{
    public interface IRateLimiter
    {
        bool AllowRequest(string key);
    }
}