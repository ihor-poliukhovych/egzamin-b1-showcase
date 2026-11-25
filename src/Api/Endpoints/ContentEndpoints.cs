using B1.Infrastructure.Content;
using Microsoft.Net.Http.Headers;

namespace B1.Api.Endpoints;

/// <summary>Exam media from the content store. Signed-in users only (fallback policy).</summary>
public static class ContentEndpoints
{
    private const string CacheControl = "private, max-age=86400";

    public static IEndpointRouteBuilder MapContent(this IEndpointRouteBuilder app)
    {
        // Range and ETag are on: the player resumes from the saved position and repeated requests are cheap.
        app.MapGet("/audio/{key}", async (string key, IContentStore store, HttpContext http, CancellationToken ct) =>
        {
            var audio = await store.OpenAudioAsync(key, ct);
            return audio is null ? Results.NotFound() : File(http, audio, enableRangeProcessing: true);
        }).WithTags("Content");

        // Task illustrations and Mowienie photos: arkusz JSON keeps "image": "zadania/<file>", the client requests /media/ + that value.
        app.MapGet("/media/{folder}/{file}", async (string folder, string file, IContentStore store, HttpContext http, CancellationToken ct) =>
        {
            if (!ContentKeys.IsValidMediaFile(file)) return Results.BadRequest();
            if (!ContentKeys.MediaFolders.Contains(folder)) return Results.NotFound();

            var image = await store.OpenMediaAsync(folder, file, ct);
            return image is null ? Results.NotFound() : File(http, image, enableRangeProcessing: false);
        }).WithTags("Content");

        return app;
    }

    private static IResult File(HttpContext http, StoredContent content, bool enableRangeProcessing)
    {
        http.Response.Headers.CacheControl = CacheControl;
        http.Response.Headers.XContentTypeOptions = "nosniff";
        var etag = new EntityTagHeaderValue($"\"{content.LastModified.ToUnixTimeSeconds():x}-{content.Length:x}\"");
        return Results.File(content.Stream, content.ContentType, lastModified: content.LastModified, entityTag: etag, enableRangeProcessing: enableRangeProcessing);
    }
}
