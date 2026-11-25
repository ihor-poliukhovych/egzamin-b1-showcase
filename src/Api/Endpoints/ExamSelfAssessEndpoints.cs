using System.Text.Json;
using B1.Api.Exam;
using B1.Api.Setup;
using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using B1.Infrastructure.Review;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace B1.Api.Endpoints;

/// <summary>Pisanie and Mowienie: the student writes or records, the AI reviewer grades on /finish.</summary>
public static class ExamSelfAssessEndpoints
{
    public const int MaxAssessmentJsonLength = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] WritingCriteria = ["wykonanie", "gramatyka", "slownictwo", "styl", "ortografia"];

    public static IEndpointRouteBuilder MapExamSelfAssess(this IEndpointRouteBuilder app)
    {
        app.MapGet("/checklists", () => Results.Ok(new { writing = Checklists.Writing, speaking = Checklists.Speaking })).WithTags("SelfAssess");

        app.MapPost("/tools/word-count", (WordCountRequest req) =>
        {
            var count = WordCounter.Count(req.Text);
            var (min, max) = WordCounter.Range(req.Target);
            return Results.Ok(new WordCountDto(count, min, max, WordCounter.Status(count, req.Target)));
        }).WithTags("SelfAssess");

        var group = app.MapGroup("/exam-attempts/{id:guid}").WithTags("SelfAssess");

        group.MapPut("/choose-set", async Task<Results<Ok<ExamAttemptDto>, NotFound, ProblemHttpResult>> (
            Guid id, ChooseSetRequest req, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var loaded = await ExamEndpoints.LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (attempt, arkusz, part) = loaded.Value;
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();
            if (!IsSelfAssessed(part) || part.Tasks.All(t => t.Number != req.Set)) return TypedResults.NotFound();

            attempt.ChosenSet = req.Set;
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ExamAttemptDto.Create(attempt, arkusz, part));
        });

        group.MapPut("/details", async Task<Results<Ok<ExamAttemptDto>, NotFound, ProblemHttpResult, ValidationProblem>> (
            Guid id, DetailsRequest req, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var json = req.Assessment.GetRawText();
            if (json.Length > MaxAssessmentJsonLength)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["assessment"] = [$"Assessment must be at most {MaxAssessmentJsonLength / 1024} KB"] },
                    extensions: new Dictionary<string, object?> { ["code"] = ProblemCodes.Validation });

            var loaded = await ExamEndpoints.LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (attempt, arkusz, part) = loaded.Value;
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();
            if (!IsSelfAssessed(part)) return TypedResults.NotFound();

            attempt.AssessmentJson = json;
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ExamAttemptDto.Create(attempt, arkusz, part));
        });

        // Task 3 (sytuacja): the partner's next line from the template, adapted by the AI to what the candidate said
        group.MapPost("/dialog/{item:int}", async Task<Results<Ok<DialogTurn>, NotFound, ProblemHttpResult, ValidationProblem>> (
            Guid id, int item, DialogTurnRequest req, AppDbContext db, CurrentUser user, DialogPartner partner, CancellationToken ct) =>
        {
            var loaded = await ExamEndpoints.LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (attempt, _, part) = loaded.Value;
            if (part.Kind != PartKind.Mowienie) return TypedResults.NotFound();
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();
            var task = part.Tasks.FirstOrDefault(t => t.Number == attempt.ChosenSet);
            if (task is null) return Conflicts.Conflict("Choose a set first");
            var examItem = task.Items.FirstOrDefault(i => i.Number == item && i.Hint == "sytuacja");
            if (examItem is null) return TypedResults.NotFound();

            var history = Dialogs.Sanitize(req.History);
            if (history.Count == 0 && !Dialogs.For(examItem.Prompt).ExaminerOpens)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["history"] = ["The candidate starts this conversation"] },
                    extensions: new Dictionary<string, object?> { ["code"] = ProblemCodes.Validation });
            return TypedResults.Ok(await partner.NextAsync(user.Id, attempt.Id, examItem.Prompt ?? string.Empty, history, ct));
        }).RequireRateLimiting(RateLimitPolicies.Ai);

        group.MapPost("/finish", async (Guid id, string? lang, AppDbContext db, CurrentUser user, TimeProvider clock, AiUsageGate usage,
            IPisanieReviewer pisanieReviewer, IMowienieReviewer mowienieReviewer, ILoggerFactory logs, CancellationToken ct) =>
        {
            var profileLanguage = Guid.TryParse(user.Id, out var userGuid)
                ? await db.Users.Where(u => u.Id == userGuid).Select(u => u.UiLanguage).FirstOrDefaultAsync(ct)
                : null;
            var language = ReviewLanguage.Normalize(lang, profileLanguage);
            var loaded = await ExamEndpoints.LoadAttemptAsync(db, id, user.Id, ct);
            if (loaded is null) return Results.NotFound();
            var (attempt, arkusz, part) = loaded.Value;
            if (attempt.IsSubmitted) return Conflicts.PartSubmitted();
            if (!IsSelfAssessed(part)) return Results.NotFound();
            var task = part.Tasks.FirstOrDefault(t => t.Number == attempt.ChosenSet);
            if (task is null) return Conflicts.Conflict("Choose a set first");

            // empty work is rejected before anything is reserved: it costs neither a model call nor a daily review
            PisanieReviewRequest? writing = null;
            MowienieReviewRequest? speaking = null;
            try
            {
                if (part.Kind == PartKind.Pisanie) writing = WritingRequest(attempt, task, language);
                else speaking = SpeakingRequest(attempt, task, language);
            }
            catch (JsonException e)
            {
                logs.CreateLogger("ExamFinish").LogWarning(e, "Attempt {AttemptId} has unreadable assessment JSON", attempt.Id);
                return ErrorHandling.ValidationProblem("assessment", "The saved answers cannot be read; save them again");
            }
            if (writing is null && speaking is null) return ErrorHandling.ReviewProblem(ReviewException.NoText);

            // one finish per attempt: of two concurrent requests only one gets past this point
            var claimedAt = clock.GetUtcNow();
            if (!await ExamEndpoints.ClaimSubmissionAsync(db, attempt.Id, claimedAt, ct)) return Conflicts.PartSubmitted();

            var finished = false;
            try
            {
                // the reservation is committed before the model call and kept whatever happens next
                if (!await usage.TryReserveAsync(user.Id, AiUsageKind.Review, attempt.Id, part.Kind, ct))
                    return ErrorHandling.ReviewProblem(ReviewException.Limit);

                decimal points;
                if (writing is not null)
                {
                    // As in the exam: the work is graded by an examiner (the AI, on the 5 criteria), there is no self-assessment
                    var review = await pisanieReviewer.ReviewAsync(writing, ct);
                    attempt.ReviewJson = JsonSerializer.Serialize(review, JsonOptions);
                    var now = clock.GetUtcNow();
                    var existing = await db.ErrorPatterns.Where(p => p.UserId == user.Id).ToListAsync(ct);
                    db.ErrorPatterns.AddRange(ReviewErrorsToBank.Apply(existing, user.Id, $"Pisanie {arkusz.Code}", review.Errors,
                        DateOnly.FromDateTime(now.UtcDateTime), AttemptSource.Pisanie, now));
                    points = SelfAssessment.WritingPoints(WritingCriteria.Select(k => review.Scores.GetValueOrDefault(k)).ToList());
                }
                else
                {
                    // Mowienie has no self-assessment either: the AI checks the transcript against the structure checklist
                    var review = await mowienieReviewer.ReviewAsync(speaking!, ct);
                    attempt.ReviewJson = JsonSerializer.Serialize(review, JsonOptions);
                    points = SpeakingPoints(task, review);
                }

                attempt.FinishSelfAssessed(points, part.MaxPoints, clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                finished = true;
                return Results.Ok(ExamAttemptDto.Create(attempt, arkusz, part));
            }
            catch (ReviewException e)
            {
                logs.CreateLogger("ExamFinish").LogWarning(e, "AI review of attempt {AttemptId} failed with {Code}", attempt.Id, e.Code);
                return ErrorHandling.ReviewProblem(e.Code);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                logs.CreateLogger("ExamFinish").LogWarning(e, "Finishing attempt {AttemptId} failed", attempt.Id);
                return ErrorHandling.ReviewProblem(ReviewException.Failed);
            }
            finally
            {
                // the attempt stays open for another try; the usage row stays as well
                if (!finished) await ExamEndpoints.ReleaseSubmissionAsync(db, attempt.Id);
            }
        }).RequireRateLimiting(RateLimitPolicies.Ai)
            .Produces<ExamAttemptDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static bool IsSelfAssessed(ExamPart part) => part.Kind is PartKind.Pisanie or PartKind.Mowienie;

    private static decimal SpeakingPoints(ExamTask task, MowienieReviewResult review) =>
        SelfAssessment.SpeakingPoints(task.Items.OrderBy(i => i.Number)
            .Select(item => SelfAssessment.SpeakingCoverage(Checklists.Speaking[item.Hint ?? string.Empty], review.Tasks?.FirstOrDefault(r => r.Task == item.Number)?.Covered))
            .ToList());

    /// <summary>One review for both tasks: examiners give one score per criterion for the whole zestaw. Null when nothing is written.</summary>
    private static PisanieReviewRequest? WritingRequest(ExamAttempt attempt, ExamTask task, string language)
    {
        var inputs = task.Items.OrderBy(i => i.Number)
            .Select(item => (Item: item, Text: attempt.FindAnswer(item.Id)?.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => new PisanieTaskInput(ZadanieLabel(x.Item.Number), x.Item.Prompt ?? string.Empty, x.Item.Hint ?? string.Empty, x.Item.TargetWords, x.Text!))
            .ToList();
        return inputs.Count == 0 ? null : new PisanieReviewRequest(inputs, language);
    }

    /// <summary>Task number in the zestaw → letter as on the arkusz: 1 → a, 2 → b.</summary>
    private static string ZadanieLabel(int number) => ((char)('a' + number - 1)).ToString();

    /// <summary>Notes and transcripts per task from AssessmentJson. Null when nothing is recorded.</summary>
    private static MowienieReviewRequest? SpeakingRequest(ExamAttempt attempt, ExamTask task, string language)
    {
        using var document = string.IsNullOrWhiteSpace(attempt.AssessmentJson) ? null : JsonDocument.Parse(attempt.AssessmentJson);
        JsonElement? assessment = document?.RootElement;
        string Read(string section, int number) =>
            assessment is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty(section, out var s) && s.ValueKind == JsonValueKind.Object
                && s.TryGetProperty(number.ToString(), out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? string.Empty
                : string.Empty;

        var inputs = task.Items.OrderBy(i => i.Number)
            .Select(i => new MowienieTaskInput(i.Number, i.Hint ?? string.Empty, i.Prompt ?? string.Empty,
                Checklists.Speaking.GetValueOrDefault(i.Hint ?? string.Empty, []), Read("notes", i.Number), Read("spoken", i.Number)))
            .ToList();
        return inputs.All(i => string.IsNullOrWhiteSpace(i.Spoken) && string.IsNullOrWhiteSpace(i.Notes)) ? null : new MowienieReviewRequest(inputs, language);
    }
}

public record DialogTurnRequest(List<DialogLine>? History);
