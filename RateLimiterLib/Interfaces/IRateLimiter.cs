namespace RateLimiterLib
{
    public interface IRateLimiter
    {
        Task<bool> AllowRequestAsync(string key);       
    }
}