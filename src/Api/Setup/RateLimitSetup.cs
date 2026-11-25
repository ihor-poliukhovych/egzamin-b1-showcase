using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace B1.Api.Setup;

public static class RateLimitPolicies
{
    /// <summary>Endpoints that call the model (/finish, /dialog): per user, one request at a time and a few per minute.</summary>
    public const string Ai = "ai";

    /// <summary>Contact form: per client IP address.</summary>
    public const string Feedback = "feedback";
}

/// <summary>
/// Rate limits. Defaults: global 300 requests/minute per user (or per IP when anonymous), "ai" = concurrency 1 + 10/minute per user,
/// "feedback" = 5/hour per IP. Section RateLimits (GlobalPerMinute, AiPerMinute, FeedbackPerHour) overrides the numbers.
/// Rejections are 429 with Retry-After and ProblemDetails code "rate-limited".
/// </summary>
public static class RateLimitSetup
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimits");
        var globalPerMinute = section.GetValue("GlobalPerMinute", 300);
        var aiPerMinute = section.GetValue("AiPerMinute", 10);
        var feedbackPerHour = section.GetValue("FeedbackPerHour", 5);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            o.AddPolicy(RateLimitPolicies.Ai, http => RateLimitPartition.Get(ClientKey(http), _ => RateLimiter.CreateChained(
                new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 }),
                new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = aiPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }))));

            o.AddPolicy(RateLimitPolicies.Feedback, http =>
                RateLimitPartition.GetFixedWindowLimiter(IpKey(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = feedbackPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));

            o.OnRejected = async (ctx, ct) =>
            {
                // a concurrency rejection has no window to wait for: a few seconds are enough
                var retryAfter = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromSeconds(5);
                ctx.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await ErrorHandling.WriteProblemAsync(ctx.HttpContext, StatusCodes.Status429TooManyRequests, ProblemCodes.RateLimited,
                    "Too many requests", "Too many requests. Please wait and try again.");
            };
        });

        return services;
    }

    private static string ClientKey(HttpContext http) =>
        http.User.Identity?.IsAuthenticated == true && http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id ? "user:" + id : IpKey(http);

    private static string IpKey(HttpContext http) => "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
