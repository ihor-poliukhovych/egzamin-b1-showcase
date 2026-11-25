using B1.Infrastructure.Content;
using B1.Infrastructure.Auth;
using B1.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Api.Tests;

/// <summary>
/// The whole Api on its own temporary SQLite database and temporary content folder. The working src/Api/b1.db is not used:
/// DbContext and IContentStore are replaced after Program's registrations, regardless of appsettings.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TeacherEmail = "teacher@example.com";
    public const string AudioKey = "test-key";
    public const string ImageFile = "test-image.png";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "b1-api-tests", Guid.NewGuid().ToString("N"));

    public string Environment { get; init; } = "Development";
    public bool DevLogin { get; init; } = true;

    public byte[] AudioBytes { get; } = Enumerable.Range(0, 1000).Select(i => (byte)(i % 251)).ToArray();
    public byte[] ImageBytes { get; } = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    /// <summary>Extra settings, e.g. lower rate limits for one test class.</summary>
    public Dictionary<string, string> Settings { get; init; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var audioRoot = Path.Combine(_directory, "audio");
        Directory.CreateDirectory(audioRoot);
        File.WriteAllBytes(Path.Combine(audioRoot, AudioKey + ".mp3"), AudioBytes);
        File.WriteAllText(Path.Combine(_directory, "secret.mp3"), "outside the audio root");
        var imageFolder = Path.Combine(audioRoot, "media", "zadania");
        Directory.CreateDirectory(imageFolder);
        File.WriteAllBytes(Path.Combine(imageFolder, ImageFile), ImageBytes);
        File.WriteAllText(Path.Combine(audioRoot, "media", "secret.png"), "outside the media folders");
        var connectionString = $"Data Source={Path.Combine(_directory, "api-test.db")};Pooling=False";

        builder.UseEnvironment(Environment);
        builder.UseSetting("Database:Provider", "sqlite");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Auth:DevLogin", DevLogin ? "true" : "false");
        builder.UseSetting("Review:Provider", "none");
        foreach (var (key, value) in Settings) builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                || d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>)
                || d.ServiceType == typeof(IContentStore)
                || d.ServiceType == typeof(AuthSettings));
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
            services.AddSingleton<IContentStore>(new LocalContentStore(audioRoot));
            services.AddSingleton(new AuthSettings { TeacherEmails = [TeacherEmail] });
        });
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public async Task<HttpClient> LoginAsync(string email)
    {
        var client = CreateBrowser();
        var response = await client.GetAsync($"/auth/dev-login?email={Uri.EscapeDataString(email)}");
        if (response.StatusCode != System.Net.HttpStatusCode.Redirect) throw new InvalidOperationException($"dev-login returned {response.StatusCode}");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll(this IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        foreach (var descriptor in services.Where(predicate).ToList()) services.Remove(descriptor);
    }
}
