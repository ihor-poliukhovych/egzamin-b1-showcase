using B1.Api.Exam;
using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using B1.Api.Setup;

namespace B1.Api.Endpoints;

public static class ExamEndpoints
{
    public static IEndpointRouteBuilder MapExam(this IEndpointRouteBuilder app)
    {
        var arkusze = app.MapGroup("/arkusze").WithTags("Arkusze");

        arkusze.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Arkusze
                .Include(a => a.Parts)
                .ThenInclude(p => p.Tasks)
                .ThenInclude(t => t.Items)
                .OrderBy(a => a.Code)
                .ToListAsync(ct);
            return Results.Ok(list.Select(ArkuszSummaryDto.Create));
        }).Produces<IEnumerable<ArkuszSummaryDto>>();

        arkusze.MapGet("/{code}", async Task<Results<Ok<ArkuszDto>, NotFound>> (string code, AppDbContext db, CancellationToken ct, PartKind partKind = PartKind.Gramatyka) =>
        {
            var arkusz = await LoadArkuszAsync(db, code, ct);
            var part = arkusz?.GetPart(partKind);
            return part is null ? TypedResults.NotFound() : TypedResults.Ok(ArkuszDto.Create(arkusz!, part));
        });

        var attempts = app.MapGroup("/exam-attempts").WithTags("ExamAttempts");

        attempts.MapGet("/", async (AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            // Sorted in memory: SQLite cannot ORDER BY a DateTimeOffset.
            var list = (await db.ExamAttempts
                    .Include(a => a.Answers)
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync(ct))
                .OrderByDescending(a => a.StartedAt)
                .ToList();
            var arkuszIds = list.Select(a => a.ArkuszId).Distinct().ToList();
            var arkuszeById = await db.Arkusze
                .Include(a => a.Parts)
                .ThenInclude(p => p.Tasks)
                .ThenInclude(t => t.Items)
                .Where(a => arkuszIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);
            return Results.Ok(list.Select(a => ExamAttemptSummaryDto.Create(a, arkuszeById[a.ArkuszId])));
        }).Produces<IEnumerable<ExamAttemptSummaryDto>>();

        attempts.MapPost("/", async Task<Results<Created<ExamAttemptDto>, NotFound, ValidationProblem>> (
            StartAttemptRequest req, AppDbContext db, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ArkuszCode))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["arkuszCode"] = ["ArkuszCode is required"] });

            var arkusz = await LoadArkuszAsync(db, req.ArkuszCode, ct);
            var part = arkusz?.GetPart(req.Part);
            if (part is null) return TypedResults.NotFound();

            var attempt = ExamAttempt.Create(user.Id, arkusz!, part, clock.GetUtcNow());
            db.ExamAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/exam-attempts/{attempt.Id}", ExamAttemptDto.Create(attempt, arkusz!, part));
        });

        attempts.MapGet("/{id:guid}", async Task<Results<Ok<ExamAttemptDto>, NotFound>> (Guid id, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var loaded = await LoadAttemptAsync(db, id, user.Id, ct);
            return loaded is null ? TypedResults.NotFound() : TypedResults.Ok(ExamAttemptDto.Create(loaded.Value.Attempt, loaded.Value.Arkusz, loaded.Value.Part));
        });

        // An abandoned, not submitted attempt can be removed (for example the time ran out long ago and the part is to be retaken).
        // Submitted attempts and attempts inside a mock exam are not deleted: their points already count.
        attempts.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound, ProblemHttpResult>> (Guid id, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var attempt = await db.ExamAttempts.Include(a => a.Answers).FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id, ct);
            if (attempt is null) return TypedResults.NotFound();
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();
            if (attempt.SessionId is not null) return Conflicts.Conflict("Attempt belongs to a mock exam session");

            db.ExamAttempts.Remove(attempt);
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        });

        attempts.MapPut("/{id:guid}/answers", async Task<Results<Ok<ExamAttemptDto>, NotFound, ProblemHttpResult, ValidationProblem>> (
            Guid id, SaveAnswersRequest req, AppDbContext db, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            var loaded = await LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (attempt, arkusz, part) = loaded.Value;
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();

            if (ValidateAnswers(req, part) is { } errors) return TypedResults.ValidationProblem(errors, extensions: ValidationCode);

            var now = clock.GetUtcNow();
            foreach (var a in req.Answers)
                attempt.SaveAnswer(a.ItemId, a.Value ?? string.Empty, now);

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ExamAttemptDto.Create(attempt, arkusz, part));
        });

        attempts.MapPut("/{id:guid}/audio-position", async Task<Results<NoContent, NotFound, ProblemHttpResult>> (
            Guid id, AudioPositionRequest req, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var loaded = await LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (attempt, _, part) = loaded.Value;
            if (part.AudioKey is null) return TypedResults.NotFound();
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();

            attempt.SaveAudioPosition(req.Seconds);
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        });

        attempts.MapPost("/{id:guid}/submit", async Task<Results<Ok<ExamAttemptDto>, NotFound, ProblemHttpResult>> (
            Guid id, AppDbContext db, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            var loaded = await LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (attempt, arkusz, part) = loaded.Value;
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();
            if (part.Kind is PartKind.Pisanie or PartKind.Mowienie) return Conflicts.Conflict("Use /finish for self-assessed parts");

            var now = clock.GetUtcNow();
            // Claim the submission in the database first: of two concurrent submits only one passes, so the error bank is filled once
            if (!await ClaimSubmissionAsync(db, attempt.Id, now, ct)) return Conflicts.PartSubmitted();
            ExamGrader.Submit(attempt, part, now);

            // Gramatyka: wrong answers go to the recurring-error bank (daily review) automatically, like errors from the Pisanie review
            if (part.Kind == PartKind.Gramatyka)
            {
                var wrong = part.Tasks.OrderBy(t => t.Number).SelectMany(t => t.Items.OrderBy(i => i.Number).Select(i => (Task: t, Item: i)))
                    .Select(x => (x.Task, x.Item, Answer: attempt.FindAnswer(x.Item.Id)))
                    .Where(x => x.Answer is { Verdict: AnswerVerdict.Wrong } && !string.IsNullOrWhiteSpace(x.Answer.Value) && x.Item.AnswerKey.Count > 0)
                    .Select(x => new ReviewError(x.Answer!.Value.Trim(), x.Item.AnswerKey[0], "błąd gramatyczny",
                        string.IsNullOrWhiteSpace(x.Item.Explanation) ? GapContext(x.Task, x.Item) : x.Item.Explanation!, null, x.Task.RomanNumber))
                    .ToList();
                if (wrong.Count > 0)
                {
                    var existing = await db.ErrorPatterns.Where(p => p.UserId == user.Id).ToListAsync(ct);
                    db.ErrorPatterns.AddRange(ReviewErrorsToBank.Apply(existing, user.Id, $"Gramatyka {arkusz.Code}", wrong,
                        DateOnly.FromDateTime(now.UtcDateTime), B1.Domain.ErrorBank.AttemptSource.Task, now));
                }
            }

            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ExamAttemptDto.Create(attempt, arkusz, part));
        });

        attempts.MapPost("/{id:guid}/answers/{itemId:guid}/accept", async Task<Results<Ok<ExamAttemptDto>, NotFound, ProblemHttpResult>> (
            Guid id, Guid itemId, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var loaded = await LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (attempt, arkusz, part) = loaded.Value;
            if (!attempt.IsSubmitted) return Conflicts.Conflict("Attempt is not submitted yet");
            if (part.FindItem(itemId) is null) return TypedResults.NotFound();

            // only a free-text answer that the key did not match can be accepted by the student
            if (!ExamGrader.Accept(attempt, part, itemId)) return Conflicts.Conflict("Only an answer that needs review can be accepted");
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ExamAttemptDto.Create(attempt, arkusz, part));
        });

        return app;
    }

    /// <summary>Context of a Gramatyka error when the key has no explanation: "Zadanie I: „…zdanie z ___…”". The model adds the rule while grouping.</summary>
    private static string GapContext(ExamTask task, ExamItem item)
    {
        var context = string.IsNullOrWhiteSpace(item.Prompt) ? task.GapSentence(item.Number) : item.Prompt.Trim();
        var prefix = ErrorGrouping.ContextRulePrefix + task.RomanNumber;
        return context is null ? prefix : $"{prefix}: „{context}”";
    }

    public const int MaxAnswerLength = 5000;

    private static readonly Dictionary<string, object?> ValidationCode = new() { ["code"] = B1.Api.Setup.ProblemCodes.Validation };

    private static Dictionary<string, string[]>? ValidateAnswers(SaveAnswersRequest req, ExamPart part)
    {
        var errors = new Dictionary<string, string[]>();
        var itemCount = part.Tasks.Sum(t => t.Items.Count);
        if (req.Answers is null) errors["answers"] = ["Answers are required"];
        else if (req.Answers.Count > itemCount) errors["answers"] = [$"At most {itemCount} answers"];
        else
        {
            var unknown = req.Answers.Where(a => part.FindItem(a.ItemId) is null).Select(a => $"Unknown itemId {a.ItemId}").ToArray();
            if (unknown.Length > 0) errors["answers"] = unknown;
            else if (req.Answers.Any(a => (a.Value?.Length ?? 0) > MaxAnswerLength)) errors["answers"] = [$"An answer must be at most {MaxAnswerLength} characters"];
        }

        return errors.Count == 0 ? null : errors;
    }

    /// <summary>Atomically marks the attempt submitted (UPDATE … WHERE SubmittedAt IS NULL); false when another request did it first.</summary>
    internal static async Task<bool> ClaimSubmissionAsync(AppDbContext db, Guid attemptId, DateTimeOffset now, CancellationToken ct) =>
        await db.ExamAttempts.Where(a => a.Id == attemptId && a.SubmittedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.SubmittedAt, now), ct) == 1;

    /// <summary>Undoes <see cref="ClaimSubmissionAsync"/> when the work after the claim failed (only the claiming request calls it).</summary>
    internal static Task ReleaseSubmissionAsync(AppDbContext db, Guid attemptId) =>
        db.ExamAttempts.Where(a => a.Id == attemptId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.SubmittedAt, (DateTimeOffset?)null), CancellationToken.None);

    private static Task<Arkusz?> LoadArkuszAsync(AppDbContext db, string code, CancellationToken ct) => db.Arkusze
        .Include(a => a.Parts)
        .ThenInclude(p => p.Tasks)
        .ThenInclude(t => t.Items)
        .FirstOrDefaultAsync(a => a.Code == code, ct);

    internal static async Task<(ExamAttempt Attempt, Arkusz Arkusz, ExamPart Part)?> LoadAttemptAsync(AppDbContext db, Guid id, string userId, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (attempt is null) return null;

        var arkusz = await db.Arkusze
            .Include(a => a.Parts)
            .ThenInclude(p => p.Tasks)
            .ThenInclude(t => t.Items)
            .FirstOrDefaultAsync(a => a.Id == attempt.ArkuszId, ct);
        var part = arkusz?.GetPart(attempt.PartKind);
        return part is null ? null : (attempt, arkusz!, part);
    }
}
