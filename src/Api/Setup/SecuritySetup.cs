using System.Net;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;

namespace B1.Api.Setup;

/// <summary>Security headers, request body limits, same-origin check for state-changing requests and trusted proxies.</summary>
public static class SecuritySetup
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; media-src 'self' blob:; " +
        "connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";

    public const long MaxRequestBodyBytes = 1024 * 1024;

    /// <summary>POST /feedback carries up to three compressed screenshots as base64.</summary>
    public const long MaxFeedbackBodyBytes = 8 * 1024 * 1024;

    /// <summary>
    /// X-Forwarded-For / X-Forwarded-Proto are trusted only from ForwardedHeaders:KnownProxies (IP addresses) and
    /// ForwardedHeaders:KnownNetworks (CIDR, e.g. 10.0.0.0/8). Empty lists trust nobody. Only one proxy hop is read.
    /// </summary>
    public static IServiceCollection AddAppSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
                o.KnownProxies.Add(IPAddress.Parse(proxy.Trim()));
            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
                o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network.Trim()));
        });
        services.AddHsts(o =>
        {
            o.MaxAge = TimeSpan.FromDays(365);
            o.IncludeSubDomains = true;
        });
        return services;
    }

    /// <summary>Headers are added when the response starts, so they are present on error responses too.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IWebHostEnvironment environment) => app.Use((ctx, next) =>
    {
        // Swagger UI (Development only) needs inline scripts
        var skipCsp = environment.IsDevelopment() && ctx.Request.Path.StartsWithSegments("/swagger");
        ctx.Response.OnStarting(() =>
        {
            var headers = ctx.Response.Headers;
            if (!skipCsp) headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "microphone=(self), camera=()";
            return Task.CompletedTask;
        });
        return next(ctx);
    });

    /// <summary>1 MB for every request, 8 MB for POST /feedback; a larger declared body is rejected with 413 before it is read.</summary>
    public static IApplicationBuilder UseRequestBodyLimits(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var limit = HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.Equals("/feedback", StringComparison.OrdinalIgnoreCase)
            ? MaxFeedbackBodyBytes
            : MaxRequestBodyBytes;
        if (ctx.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature) feature.MaxRequestBodySize = limit;
        if (ctx.Request.ContentLength > limit)
        {
            await ErrorHandling.WriteProblemAsync(ctx, StatusCodes.Status413PayloadTooLarge, ProblemCodes.PayloadTooLarge,
                "Payload too large", $"The request body must not exceed {limit / 1024} KB.");
            return;
        }

        await next(ctx);
    });

    /// <summary>
    /// CSRF defence in depth (the cookie is already SameSite=Lax): a POST/PUT/PATCH/DELETE whose Origin header is present
    /// and is neither this site nor a configured Cors:Origins entry is rejected with 403.
    /// </summary>
    public static IApplicationBuilder UseSameOriginCheck(this IApplicationBuilder app, IReadOnlyCollection<string> allowedOrigins) => app.Use(async (ctx, next) =>
    {
        var method = ctx.Request.Method;
        var stateChanging = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
        var origin = ctx.Request.Headers.Origin.ToString();
        if (stateChanging && origin.Length > 0)
        {
            var self = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var allowed = string.Equals(origin, self, StringComparison.OrdinalIgnoreCase)
                || allowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
            if (!allowed)
            {
                await ErrorHandling.WriteProblemAsync(ctx, StatusCodes.Status403Forbidden, ProblemCodes.ForbiddenOrigin,
                    "Forbidden", "Cross-origin requests are not allowed.");
                return;
            }
        }

        await next(ctx);
    });

    /// <summary>www.egzamin-b1.pl → egzamin-b1.pl: one site address (sign-in cookie, OAuth return, links). 301 is a permanent redirect.</summary>
    public static IApplicationBuilder UseBareDomainRedirect(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var host = ctx.Request.Host;
        if (host.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            var target = new UriBuilder("https", host.Host[4..]) { Port = -1, Path = ctx.Request.PathBase + ctx.Request.Path, Query = ctx.Request.QueryString.Value }.Uri;
            ctx.Response.Redirect(target.AbsoluteUri, permanent: true);
            return;
        }

        await next(ctx);
    });
}
