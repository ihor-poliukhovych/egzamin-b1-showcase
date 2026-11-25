using System.Security.Claims;
using B1.Api.Endpoints;
using B1.Domain.Auth;
using B1.Infrastructure.Auth;
using B1.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace B1.Api.Setup;

/// <summary>Cookie sign-in, optional Google sign-in and the role policies. Every endpoint requires a signed-in user unless it allows anonymous access.</summary>
public static class AuthSetup
{
    public const string DevelopmentCookieName = "b1.auth";

    /// <summary>The __Host- prefix makes the browser require Secure, Path=/ and no Domain attribute.</summary>
    public const string ProductionCookieName = "__Host-b1.auth";

    public static IServiceCollection AddAppAuth(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var authBuilder = services
            .AddAuthentication(o => o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = environment.IsDevelopment() ? DevelopmentCookieName : ProductionCookieName;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromDays(14);
                o.SlidingExpiration = true;
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
                o.Events.OnValidatePrincipal = ValidatePrincipalAsync;
            });

        var googleClientId = configuration["Auth:Google:ClientId"];
        var googleClientSecret = configuration["Auth:Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            authBuilder.AddGoogle(AuthEndpoints.GoogleScheme, o =>
            {
                o.ClientId = googleClientId;
                o.ClientSecret = googleClientSecret;
                o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.CallbackPath = "/signin-google";
                o.Events.OnCreatingTicket = async ctx =>
                {
                    var identity = ctx.Identity ?? throw new InvalidOperationException("Google did not return an identity");
                    var subject = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new InvalidOperationException("Google did not return a subject");
                    var email = identity.FindFirst(ClaimTypes.Email)?.Value ?? throw new InvalidOperationException("Google did not return an email");
                    var name = identity.FindFirst(ClaimTypes.Name)?.Value ?? email;
                    var user = await ctx.HttpContext.RequestServices.GetRequiredService<UserProvisioner>().FindOrCreateAsync(subject, email, name, ctx.HttpContext.RequestAborted);
                    AuthEndpoints.ApplyClaims(identity, user);
                };
            });
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(nameof(UserRole.Student), p => p.RequireRole(nameof(UserRole.Student), nameof(UserRole.Teacher)))
            .AddPolicy(nameof(UserRole.Teacher), p => p.RequireRole(nameof(UserRole.Teacher)));

        return services;
    }

    /// <summary>
    /// The cookie lives 14 days, but the account may have been deleted or the role changed from another device,
    /// so every request is checked against the database.
    /// </summary>
    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext ctx)
    {
        var principal = ctx.Principal;
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = Guid.TryParse(id, out var guid)
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == guid, ctx.HttpContext.RequestAborted)
            : null;
        if (principal is null || user is null)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (principal.FindFirstValue(ClaimTypes.Role) != user.Role.ToString() && principal.Identity is ClaimsIdentity identity)
        {
            AuthEndpoints.ApplyClaims(identity, user);
            ctx.ShouldRenew = true;
        }
    }
}
