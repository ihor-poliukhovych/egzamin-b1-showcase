using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Infrastructure.Content;

public record StoredContent(Stream Stream, string ContentType, long Length, DateTimeOffset LastModified);

/// <summary>
/// Store for exam media that is not part of the code: Sluchanie recordings and task images/photos.
/// Content is loaded by the operator; the repository ships none. Today a local folder; a cloud implementation
/// (Azure Blob, S3, GCS) is added here together with a new Content:Store value.
/// </summary>
public interface IContentStore
{
    /// <summary>Recording <c>&lt;root&gt;/&lt;key&gt;.mp3</c>; null for an invalid key or a missing file.</summary>
    Task<StoredContent?> OpenAudioAsync(string key, CancellationToken ct = default);

    /// <summary>Image <c>&lt;root&gt;/media/&lt;folder&gt;/&lt;file&gt;</c>; null when the folder is unknown or the file is missing.</summary>
    Task<StoredContent?> OpenMediaAsync(string folder, string file, CancellationToken ct = default);
}

public static partial class ContentKeys
{
    /// <summary>Media folders: task illustrations and Mowienie photos.</summary>
    public static readonly IReadOnlySet<string> MediaFolders = new HashSet<string>(StringComparer.Ordinal) { "zadania", "zdjecia" };

    [GeneratedRegex(@"^[A-Za-z0-9-]{4,40}$")]
    public static partial Regex AudioKey();

    [GeneratedRegex(@"^[A-Za-z0-9-]{1,60}\.(jpg|jpeg|png|webp)$")]
    public static partial Regex MediaFile();

    public static bool IsValidMediaFile(string file) => MediaFile().IsMatch(file);

    public static string ImageContentType(string file) => Path.GetExtension(file) switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}

public class LocalContentStore : IContentStore
{
    private readonly string _root;

    public LocalContentStore(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public Task<StoredContent?> OpenAudioAsync(string key, CancellationToken ct = default)
    {
        if (!ContentKeys.AudioKey().IsMatch(key)) return Task.FromResult<StoredContent?>(null);
        return Task.FromResult(Open(Path.Combine(_root, key + ".mp3"), "audio/mpeg"));
    }

    public Task<StoredContent?> OpenMediaAsync(string folder, string file, CancellationToken ct = default)
    {
        if (!ContentKeys.MediaFolders.Contains(folder) || !ContentKeys.IsValidMediaFile(file)) return Task.FromResult<StoredContent?>(null);
        return Task.FromResult(Open(Path.Combine(_root, "media", folder, file), ContentKeys.ImageContentType(file)));
    }

    private StoredContent? Open(string path, string contentType)
    {
        // the key patterns already exclude separators and dots; this is a second guard against leaving the root
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(full)) return null;

        var info = new FileInfo(full);
        var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return new StoredContent(stream, contentType, info.Length, info.LastWriteTimeUtc);
    }
}

public static class ContentRegistration
{
    public const string StoreKey = "Content:Store";
    public const string LocalPathKey = "Content:LocalPath";

    /// <summary>
    /// Content:Store = local (default). Content:LocalPath is the root folder: recordings at the root, images under media/zadania
    /// and media/zdjecia. The older keys Audio:Store and Audio:LocalPath are still read as fallbacks.
    /// </summary>
    public static IServiceCollection AddContentStore(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        var store = (configuration[StoreKey] ?? configuration["Audio:Store"] ?? "local").Trim().ToLowerInvariant();
        switch (store)
        {
            case "local":
                var configured = configuration[LocalPathKey] ?? configuration["Audio:LocalPath"];
                var root = !string.IsNullOrWhiteSpace(configured) ? configured : FindDefaultRoot(contentRoot);
                services.AddSingleton<IContentStore>(new LocalContentStore(root));
                break;
            default:
                throw new InvalidOperationException($"Unknown {StoreKey}: '{store}'. Use 'local' (cloud storage implementations go here).");
        }

        return services;
    }

    private static string FindDefaultRoot(string contentRoot)
    {
        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content-local", "audio");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return Path.Combine(contentRoot, "content-local", "audio");
    }
}
