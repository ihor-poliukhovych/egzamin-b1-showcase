using B1.Infrastructure.Persistence;
using B1.Infrastructure.Seed;

namespace B1.Api.Cli;

/// <summary>
/// <c>dotnet run --project src/Api -- seed-arkusze [--from &lt;dir&gt;]</c>: imports every *.json arkusz from the folder into the database
/// (insert or update by code). The default folder is content/arkusze in the repository root. The repository ships no exam
/// content: the operator supplies the files (format: content/arkusze/README.md).
/// </summary>
public static class SeedArkuszeCommand
{
    public const string Name = "seed-arkusze";

    public static bool Matches(string[] args) => args.Length > 0 && args[0].Equals(Name, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var from = Option(args, "--from");
        var directory = from is not null ? Path.GetFullPath(from) : DefaultDirectory(app.Environment.ContentRootPath);
        if (!Directory.Exists(directory))
        {
            app.Logger.LogError("Seed arkusze: folder {Directory} does not exist; pass --from <dir>", directory);
            return 1;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().InitializeAsync();
        var result = await scope.ServiceProvider.GetRequiredService<ArkuszImporter>().ImportDirectoryAsync(directory);
        app.Logger.LogInformation("Seed arkusze {Directory}: inserted {Inserted}, updated {Updated}: {Codes}",
            directory, result.Inserted, result.Updated, string.Join(", ", result.Codes));
        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>content/arkusze in the repository root: the first folder up from the content root that contains EgzaminB1.sln.</summary>
    private static string DefaultDirectory(string contentRoot)
    {
        for (var dir = new DirectoryInfo(contentRoot); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "EgzaminB1.sln"))) return Path.Combine(dir.FullName, "content", "arkusze");
        return Path.Combine(contentRoot, "content", "arkusze");
    }
}
