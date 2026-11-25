using System.Security.Claims;
using B1.Domain.Feedback;
using B1.Api.Setup;
using B1.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace B1.Api.Endpoints;

/// <param name="Images">up to three base64 images (data URLs allowed); the browser compresses them to about 1600 px JPEG first</param>
public record FeedbackRequest(FeedbackTopic Topic, string? Message, string? Email, string? Language, string? Website, List<string>? Images = null);

public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedback(this IEndpointRouteBuilder app)
    {
        // The contact form works without signing in: anonymous senders share an hourly limit, signed-in users have their own.
        // On top of that the "feedback" rate limit allows 5 requests per hour per IP address.
        app.MapPost("/feedback", async Task<Results<NoContent, ValidationProblem, StatusCodeHttpResult>> (
            FeedbackRequest req, HttpContext http, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            // bot trap: only bots fill in the hidden field; answer "ok" and store nothing
            if (!string.IsNullOrWhiteSpace(req.Website)) return TypedResults.NoContent();

            var userId = http.User.Identity?.IsAuthenticated == true ? http.User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            var now = clock.GetUtcNow();
            // Counted in memory: SQLite cannot compare DateTimeOffset in a query.
            var recent = (await db.FeedbackMessages.Where(f => f.UserId == userId).Select(f => f.CreatedAt).ToListAsync(ct))
                .Count(at => at > now.AddHours(-1));
            if (FeedbackMessage.LimitReached(userId is null, recent)) return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);

            var images = (req.Images ?? []).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if (images.Count > FeedbackAttachment.MaxPerMessage)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["images"] = [$"Up to {FeedbackAttachment.MaxPerMessage} images"] });
            if (images.Count > 0)
            {
                var today = (await db.FeedbackAttachments.Select(a => a.CreatedAt).ToListAsync(ct)).Count(at => at > now.AddDays(-1));
                if (today + images.Count > FeedbackAttachment.DailySiteLimit) return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            FeedbackMessage message;
            var attachments = new List<FeedbackAttachment>();
            try
            {
                message = FeedbackMessage.Create(userId, req.Topic, req.Message ?? string.Empty, req.Email, req.Language, now);
                foreach (var image in images) attachments.Add(FeedbackAttachment.Create(message.Id, DecodeImage(image), now));
            }
            catch (ArgumentException e)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [e.ParamName == "data" ? "images" : e.ParamName ?? "message"] = [e.Message] });
            }

            db.FeedbackMessages.Add(message);
            db.FeedbackAttachments.AddRange(attachments);
            await db.SaveChangesAsync(ct);

            // retention period from the privacy policy: messages older than RetentionMonths are deleted
            var expired = (await db.FeedbackMessages.Select(f => new { f.Id, f.CreatedAt }).ToListAsync(ct))
                .Where(f => f.CreatedAt < now.AddMonths(-FeedbackMessage.RetentionMonths)).Select(f => f.Id).ToList();
            if (expired.Count > 0)
            {
                await db.FeedbackAttachments.Where(a => expired.Contains(a.MessageId)).ExecuteDeleteAsync(ct);
                await db.FeedbackMessages.Where(f => expired.Contains(f.Id)).ExecuteDeleteAsync(ct);
            }
            return TypedResults.NoContent();
        }).WithTags("Feedback").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Feedback);

        return app;
    }

    /// <summary>base64 or data URL → bytes; anything else throws ArgumentException (becomes a ValidationProblem).</summary>
    private static byte[] DecodeImage(string value)
    {
        var comma = value.StartsWith("data:", StringComparison.Ordinal) ? value.IndexOf(',') : -1;
        var base64 = comma >= 0 ? value[(comma + 1)..] : value;
        if (base64.Length > FeedbackAttachment.MaxBytes / 3 * 4 + 8) throw new ArgumentException("Image is too large", "data");
        try { return Convert.FromBase64String(base64); }
        catch (FormatException) { throw new ArgumentException("Image is not base64", "data"); }
    }
}
