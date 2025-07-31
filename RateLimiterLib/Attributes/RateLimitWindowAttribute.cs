using Microsoft.AspNetCore.Mvc.Filters;

namespace RateLimiterLib;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RateLimitWindowAttribute : Attribute, IAsyncActionFilter
{
    private readonly TimeSpan _start;
    private readonly TimeSpan _end;
    private readonly bool _weekendsAllowed;
    private readonly HashSet<DateTime> _holidays;

    public RateLimitWindowAttribute(string startTime, string endTime, bool weekendsAllowed = false, params string[] holidays)
    {
        _start = TimeSpan.Parse(startTime);
        _end = TimeSpan.Parse(endTime);
        _weekendsAllowed = weekendsAllowed;
        _holidays = new HashSet<DateTime>(holidays.Select(d => DateTime.Parse(d).Date));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var now = DateTime.UtcNow.TimeOfDay;
        var today = DateTime.UtcNow.Date;
        var dayOfWeek = DateTime.UtcNow.DayOfWeek;

        bool isHoliday = _holidays.Contains(today);
        bool isWeekend = (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday);

        if (now >= _start && now <= _end && (!_weekendsAllowed || !isWeekend) && !isHoliday)
        {
            // Within rate limit window - continue
            await next();
        }
        else
        {
            // Outside allowed window, skip rate limiting (or block)
            // Here we allow execution without rate limiting:
            await next();
        }
    }
}
