using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages; // For RazorPages types
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Reflection; // For MethodInfo
using System.Threading.Tasks;
using RateLimiterLib.Enums;

namespace RateLimiterLib
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RateLimitAttribute : Attribute, IAsyncActionFilter, IAsyncPageFilter
    {
        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _requestLog = new();

        private readonly int _maxAttempts;
        private readonly TimeSpan _window;

        public RateLimitAttribute(
            int maxAttempts = 10,
            int seconds = 60, 
            RateLimitStrategy strategy = RateLimitStrategy.FixedWindow)
        {
            _maxAttempts = maxAttempts;
            _window = TimeSpan.FromSeconds(seconds);
        }

        // For MVC Controllers
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<RateLimitAttribute>)) as ILogger;
            var identifier = context.HttpContext.User.Identity?.Name
                             ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
                             ?? "anonymous";

            var actionName = context.ActionDescriptor?.DisplayName
                             ?? context.ActionDescriptor?.RouteValues["action"]
                             ?? "unknown_action";

            var key = $"{identifier}:{actionName}";

            if (IsLimitExceeded(key, logger, actionName, identifier, out var result))
            {
                context.Result = result;
                return;
            }

            await next();
        }

        // For Razor Pages
        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<RateLimitAttribute>)) as ILogger;
            var identifier = context.HttpContext.User.Identity?.Name
                             ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
                             ?? "anonymous";

            // context.HandlerMethod is of type MethodInfo (from System.Reflection)
            var handlerMethodDescriptor = context.HandlerMethod; // This is HandlerMethodDescriptor
            var methodInfo = handlerMethodDescriptor?.MethodInfo;
            var actionName = methodInfo != null 
                ? $"{methodInfo.DeclaringType?.Name ?? "UnknownType"}.{methodInfo.Name}" 
                : "UnknownHandler";

            var key = $"{identifier}:{actionName}";

            if (IsLimitExceeded(key, logger, actionName, identifier, out var result))
            {
                context.Result = result;
                return;
            }

            await next();
        }

        // No logic needed here for Razor Pages
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

        private bool IsLimitExceeded(string key, ILogger logger, string actionName, string userIdentifier, out IActionResult result)
        {
            var now = DateTime.UtcNow;

            _requestLog.AddOrUpdate(key,
                addValueFactory: (_) => (1, now),
                updateValueFactory: (_, old) =>
                {
                    if (now - old.WindowStart > _window)
                    {
                        return (1, now);
                    }
                    else
                    {
                        return (old.Count + 1, old.WindowStart);
                    }
                });

            var current = _requestLog[key];

            logger?.LogInformation("RateLimit: User {User} has made {Count} requests for {Action}. Limit is {Limit} in {Seconds} seconds.",
                userIdentifier, current.Count, actionName, _maxAttempts, _window.TotalSeconds);

            if (current.Count > _maxAttempts)
            {
                logger?.LogWarning("RateLimit: User {User} exceeded the limit on {Action}.", userIdentifier, actionName);

                result = new ContentResult
                {
                    StatusCode = 429,
                    Content = "Too many requests. Please try again later."
                };
                return true;
            }

            result = null;
            return false;
        }
    }
}
