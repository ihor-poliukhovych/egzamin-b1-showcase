using B1.Api.Endpoints;

namespace B1.Api.Setup;

public static class PipelineSetup
{
    /// <summary>Middleware order: proxy headers → security headers → errors → HSTS → www redirect → body limits → static front end → CORS → auth → origin check → rate limits.</summary>
    public static WebApplication UseAppPipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) app.UseForwardedHeaders();
        app.UseSecurityHeaders(app.Environment);
        app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseBareDomainRedirect();
        app.UseRequestBodyLimits();

        // The front end is built into wwwroot: it is served before authorisation, the sign-in screen itself is public.
        app.UseDefaultFiles();
        // index.html is always revalidated (a new version right after deploy); /assets files with a hash in the name are cached for long
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var path = ctx.Context.Request.Path.Value ?? string.Empty;
                ctx.Context.Response.Headers.CacheControl = path.StartsWith("/assets/", StringComparison.Ordinal)
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";
            },
        });

        app.UseCors();
        app.UseAuthentication();
        app.UseSameOriginCheck(ServiceSetup.CorsOrigins(app.Configuration));
        app.UseRateLimiter();
        app.UseAuthorization();

        // Swagger is middleware outside authorisation, so it is served only in Development
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }

    public static WebApplication MapAppEndpoints(this WebApplication app)
    {
        // A built front end (wwwroot/index.html, as in the container) is served directly in every environment.
        var hasBuiltFrontend = app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;

        if (app.Environment.IsDevelopment() && !hasBuiltFrontend)
        {
            // Without a build, the browser returns to the API root after sign-in; send it on to the Vite dev server. Swagger stays at /swagger.
            var devFrontend = ServiceSetup.CorsOrigins(app.Configuration).FirstOrDefault() ?? "http://localhost:5173";
            app.MapGet("/", () => Results.Redirect(devFrontend + "/")).ExcludeFromDescription().AllowAnonymous();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("System").AllowAnonymous();
        app.MapAuth();
        app.MapSite();
        app.MapContent();
        app.MapGroups();
        app.MapDashboards();
        app.MapPatterns();
        app.MapReview();
        app.MapExam();
        app.MapExamSessions();
        app.MapExamSelfAssess();
        app.MapFeedback();

        if (hasBuiltFrontend)
            app.MapFallbackToFile("index.html", new StaticFileOptions { OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache" }).AllowAnonymous();

        return app;
    }
}
