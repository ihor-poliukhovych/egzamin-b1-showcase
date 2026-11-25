using System.Security.Claims;
using B1.Domain.Auth;
using B1.Infrastructure.Auth;
using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using B1.Api.Setup;

namespace B1.Api.Endpoints;

public record MeDto(string Id, string Email, string DisplayName, string Role, string TargetLevel, string TargetVariant, string UiLanguage, bool RoleChosen, DateOnly? ExamDate = null);

public record ExamDateRequest(DateOnly? Date);

public record RoleRequest(string Role);

public record SettingsRequest(string TargetLevel, string TargetVariant, string? UiLanguage = null);

public static class AuthEndpoints
{
    public const string GoogleScheme = "Google";

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth").AllowAnonymous();

        group.MapGet("/login", (string? returnUrl, IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(configuration["Auth:Google:ClientId"]) || string.IsNullOrWhiteSpace(configuration["Auth:Google:ClientSecret"]))
                return Results.Problem("Google sign-in is not configured: set Auth:Google:ClientId and Auth:Google:ClientSecret.", statusCode: 501);

            return Results.Challenge(new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) }, [GoogleScheme]);
        });

        group.MapGet("/dev-login", async (string? email, string? returnUrl, HttpContext http, IWebHostEnvironment env, IConfiguration configuration, UserProvisioner provisioner, CancellationToken ct) =>
        {
            if (!DevLoginEnabled(env, configuration)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Valid email is required"] });

            var user = await provisioner.FindOrCreateAsync($"dev:{email.Trim().ToLowerInvariant()}", email, email.Split('@')[0], ct);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(CreateIdentity(user)));
            return Results.Redirect(SafeReturnUrl(returnUrl));
        });

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        app.MapGet("/auth/me", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var user = Guid.TryParse(id, out var guid) ? await db.Users.FirstOrDefaultAsync(u => u.Id == guid, ct) : null;
            return Results.Ok(new MeDto(id, principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty, principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                user?.Role.ToString() ?? principal.FindFirstValue(ClaimTypes.Role) ?? nameof(UserRole.Student), user?.TargetLevel ?? "B1", user?.TargetVariant ?? "dorosli", user?.UiLanguage ?? User.DefaultUiLanguage,
                user?.RoleChosen ?? true, user?.ExamDate));
        }).WithTags("Auth");

        // First sign-in: the user says whether they are a student or a teacher. The choice is made once.
        app.MapPost("/auth/role", async (RoleRequest req, HttpContext http, ClaimsPrincipal principal, AppDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: false, out var role) || !Enum.IsDefined(role)) return Results.BadRequest("Unknown role");
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();
            if (!user.ChooseRole(role, time.GetUtcNow())) return Conflicts.Conflict("Role already chosen");

            await db.SaveChangesAsync(ct);
            // the role in the cookie must match the database right away, without signing in again
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(CreateIdentity(user)));
            return Results.NoContent();
        }).WithTags("Auth");

        app.MapPut("/auth/role", async (RoleRequest req, HttpContext http, ClaimsPrincipal principal, AppDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            if (!Enum.TryParse<UserRole>(req.Role, ignoreCase: false, out var role) || !Enum.IsDefined(role)) return Results.BadRequest("Unknown role");
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();

            user.ChangeRole(role, time.GetUtcNow());
            await db.SaveChangesAsync(ct);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(CreateIdentity(user)));
            return Results.NoContent();
        }).WithTags("Auth");

        app.MapPut("/auth/exam-date", async (ExamDateRequest req, ClaimsPrincipal principal, AppDbContext db, TimeProvider time, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();
            if (!user.SetExamDate(req.Date, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime))) return Results.BadRequest("Exam date must not be in the past");

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Auth");

        app.MapPut("/auth/settings", async (SettingsRequest req, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            string[] levels = ["A2", "B1", "B2", "C1", "C2"];
            string[] variants = ["dorosli", "dim"];
            if (!levels.Contains(req.TargetLevel) || !variants.Contains(req.TargetVariant)) return Results.BadRequest("Unknown level or variant");
            if (req.UiLanguage is not null && !User.UiLanguages.Contains(req.UiLanguage)) return Results.BadRequest("Unknown language");
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return Results.NotFound();

            user.TargetLevel = req.TargetLevel;
            user.TargetVariant = req.TargetVariant;
            if (req.UiLanguage is not null) user.UiLanguage = req.UiLanguage;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithTags("Auth");

        // GDPR: deletes the account and all of the user's data; the client confirms the action beforehand
        app.MapDelete("/auth/account", async (HttpContext http, ClaimsPrincipal principal, AccountService accounts, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Results.Unauthorized();
            var deleted = await accounts.DeleteAsync(id, ct);
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithTags("Auth");

        return app;
    }

    public static ClaimsIdentity CreateIdentity(User user, string scheme = CookieAuthenticationDefaults.AuthenticationScheme)
    {
        var identity = new ClaimsIdentity(scheme);
        ApplyClaims(identity, user);
        return identity;
    }

    public static void ApplyClaims(ClaimsIdentity identity, User user)
    {
        foreach (var type in new[] { ClaimTypes.NameIdentifier, ClaimTypes.Email, ClaimTypes.Name, ClaimTypes.Role })
            foreach (var claim in identity.FindAll(type).ToList()) identity.RemoveClaim(claim);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.UserKey));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
    }

    /// <summary>Dev login is available only in Development with Auth:DevLogin = true (appsettings.Development.json).</summary>
    public static bool DevLoginEnabled(IWebHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration.GetValue("Auth:DevLogin", false);

    /// <summary>Open-redirect guard: only a local path is accepted, otherwise "/".</summary>
    public static string SafeReturnUrl(string? returnUrl) => IsLocalUrl(returnUrl) ? returnUrl! : "/";

    /// <summary>
    /// The rules of IUrlHelper.IsLocalUrl for a path: starts with "/" but not "//" or "/" followed by a backslash, and contains
    /// no control characters (browsers drop tabs and new lines, so "/&lt;tab&gt;/evil.example" would become "//evil.example").
    /// </summary>
    public static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/') return false;
        if (url.Length == 1) return true;
        if (url[1] is '/' or '\\') return false;
        return !url.Any(char.IsControl);
    }
}
