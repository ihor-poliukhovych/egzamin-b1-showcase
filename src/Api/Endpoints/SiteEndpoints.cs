namespace B1.Api.Endpoints;

/// <summary>
/// Site operator details for the public pages (privacy policy, terms, contact), taken from configuration Site:*, not from code.
/// DevLogin tells the front end to show the "Dev login" button (Development with Auth:DevLogin = true only).
/// </summary>
public record SiteDto(string Name, string OwnerName, string ContactEmail, bool DevLogin = false);

public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSite(this IEndpointRouteBuilder app)
    {
        app.MapGet("/site", (IConfiguration configuration, IWebHostEnvironment environment) => Results.Ok(new SiteDto(
            Name: Value(configuration, "Site:Name", "Polski B1"),
            OwnerName: Value(configuration, "Site:OwnerName", "[imię i nazwisko administratora]"),
            ContactEmail: Value(configuration, "Site:ContactEmail", "kontakt@example.com"),
            DevLogin: AuthEndpoints.DevLoginEnabled(environment, configuration))))
            .WithTags("Site").AllowAnonymous();
        return app;
    }

    private static string Value(IConfiguration configuration, string key, string fallback) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? fallback : configuration[key]!.Trim();
}
