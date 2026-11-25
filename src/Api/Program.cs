using B1.Api.Cli;
using B1.Api.Setup;
using B1.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
// Secrets come from the environment, e.g. EGZAMINB1_Review__ApiKey → Review:ApiKey
builder.Configuration.AddEnvironmentVariables("EGZAMINB1_");

builder.Services.AddAppServices(builder.Configuration, builder.Environment);
builder.Services.AddAppAuth(builder.Configuration, builder.Environment);
builder.Services.AddAppRateLimiting(builder.Configuration);
builder.Services.AddAppErrorHandling();
builder.Services.AddAppSecurity(builder.Configuration);

var app = builder.Build();

if (SeedArkuszeCommand.Matches(args))
{
    Environment.ExitCode = await SeedArkuszeCommand.RunAsync(app, args);
    return;
}

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().InitializeAsync();
}

app.UseAppPipeline();
app.MapAppEndpoints();
app.Run();

/// <summary>Entry point, visible to WebApplicationFactory in tests/Api.Tests.</summary>
public partial class Program { }
