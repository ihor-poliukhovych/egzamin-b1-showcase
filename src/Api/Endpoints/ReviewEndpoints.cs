using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using B1.Infrastructure.Review;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace B1.Api.Endpoints;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReview(this IEndpointRouteBuilder app)
    {
        app.MapGet("/today", async (string? lang, AppDbContext db, CurrentUser user, TimeProvider clock, IErrorGrouper grouper, AiUsageGate usage,
            ErrorGroupingBackoff backoff, ILoggerFactory logs, CancellationToken ct) =>
        {
            var today = Today(clock);
            var profileLanguage = Guid.TryParse(user.Id, out var userGuid)
                ? await db.Users.Where(u => u.Id == userGuid).Select(u => u.UiLanguage).FirstOrDefaultAsync(ct)
                : null;
            await GroupPendingAsync(db, grouper, usage, backoff, logs.CreateLogger("ErrorGrouping"), user.Id, ReviewLanguage.Normalize(lang, profileLanguage), ct);
            var session = await GetOrPlanSessionAsync(db, user.Id, today, ct);
            return await BuildTodayAsync(db, session, ct);
        }).WithTags("Review");

        app.MapPost("/attempts", async Task<Results<Ok<AttemptResponse>, NotFound, ValidationProblem>> (
            AttemptRequest req, AppDbContext db, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            var date = req.Date ?? Today(clock);
            var pattern = await db.ErrorPatterns
                .Include(p => p.Attempts)
                .FirstOrDefaultAsync(p => p.Id == req.PatternId && p.UserId == user.Id, ct);
            if (pattern is null) return TypedResults.NotFound();

            var attempt = pattern.Record(req.Result, date, req.Source, req.Evidence, req.CountsTowardStreak);

            var session = await GetOrPlanSessionAsync(db, user.Id, date, ct);
            session.Answer(pattern.Id, req.Result);

            await db.SaveChangesAsync(ct);

            var today = await BuildTodayAsync(db, session, ct);
            return TypedResults.Ok(new AttemptResponse(attempt.Id, PatternDto.From(pattern), today));
        }).WithTags("Review");

        return app;
    }

    /// <summary>
    /// New errors (from Pisanie, Gramatyka) are sorted into types by the AI, one call per batch of at most
    /// <see cref="ErrorGrouping.BatchSize"/>. Each call counts as AI usage (daily limit Review:DailyLimit). An AI failure does not break
    /// the daily review: the errors stay ungrouped and are retried, but not earlier than <see cref="ErrorGroupingBackoff.Delay"/> later.
    /// </summary>
    private static async Task GroupPendingAsync(AppDbContext db, IErrorGrouper grouper, AiUsageGate usage, ErrorGroupingBackoff backoff, ILogger logger,
        string userId, string language, CancellationToken ct)
    {
        if (grouper.UsesModel && backoff.IsBlocked(userId)) return;

        var patterns = await db.ErrorPatterns.Where(p => p.UserId == userId).ToListAsync(ct);
        var pending = ErrorGrouping.Pending(patterns);
        if (pending.Count == 0) return;

        var existing = ErrorGrouping.ExistingGroups(patterns);
        try
        {
            if (grouper.UsesModel && !await usage.TryReserveAsync(userId, AiUsageKind.Grouping, Guid.Empty, PartKind.None, ct)) return;
            var assignments = await grouper.GroupAsync(ErrorGrouping.ToItems(pending), existing, language, ct);
            ErrorGrouping.Apply(pending, existing, assignments);
            await db.SaveChangesAsync(ct);
            backoff.Succeeded(userId);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            backoff.Failed(userId);
            logger.LogWarning(e, "Error grouping failed for {Count} patterns", pending.Count);
        }
    }

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    /// <summary>The daily review is planned once and does not change afterwards, even when pattern counters move.</summary>
    private static async Task<ReviewSession> GetOrPlanSessionAsync(AppDbContext db, string userId, DateOnly date, CancellationToken ct)
    {
        var session = await db.ReviewSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Date == date, ct);
        var patterns = await db.ErrorPatterns
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);
        var blitz = BlitzSelector.Select(patterns, date);

        if (session is not null)
        {
            // The day plan is fixed, but new errors (from the AI review and so on) are appended while there is room
            if (session.AddPlanned(blitz.Select(p => p.Id), ReviewPolicy.BlitzMax) > 0) await db.SaveChangesAsync(ct);
            return session;
        }

        session = new ReviewSession(userId, date, blitz.Select(p => p.Id));
        db.ReviewSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    private static async Task<TodayDto> BuildTodayAsync(AppDbContext db, ReviewSession session, CancellationToken ct)
    {
        var ids = session.Items.Select(i => i.PatternId).ToList();
        var patterns = await db.ErrorPatterns.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var items = session.Items
            .OrderBy(i => i.Order)
            .Where(i => patterns.ContainsKey(i.PatternId))
            .Select(i => new TodayItemDto(PatternDto.From(patterns[i.PatternId]), i.Result, i.Order))
            .ToList();

        return new TodayDto(session.Id, session.Date, items.All(i => i.Result is not null),
            items.Count(i => i.Result is not null), items.Count, items);
    }
}
