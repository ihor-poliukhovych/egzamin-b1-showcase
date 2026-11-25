using B1.Api.Exam;
using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using B1.Api.Setup;

namespace B1.Api.Endpoints;

public record ReusablePartDto(PartKind Part, Guid AttemptId, DateTimeOffset SubmittedAt, decimal Points, decimal MaxPoints);

public record AdoptPartRequest(Guid AttemptId);

public static class ExamSessionEndpoints
{
    public static IEndpointRouteBuilder MapExamSessions(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/exam-sessions").WithTags("ExamSessions");

        group.MapGet("/", async (AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var sessions = (await db.ExamSessions.Where(s => s.UserId == user.Id).ToListAsync(ct))
                .OrderByDescending(s => s.StartedAt)
                .ToList();
            var result = new List<SessionDto>();
            foreach (var session in sessions)
            {
                var loaded = await LoadAsync(db, session.Id, user.Id, ct);
                if (loaded is not null) result.Add(SessionDto.Create(loaded.Value.Session, loaded.Value.Arkusz, loaded.Value.Attempts));
            }

            return Results.Ok(result);
        }).Produces<IEnumerable<SessionDto>>();

        group.MapPost("/", async Task<Results<Created<SessionDto>, NotFound, ProblemHttpResult>> (
            StartSessionRequest req, AppDbContext db, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            var arkusz = await db.Arkusze
                .Include(a => a.Parts)
                .ThenInclude(p => p.Tasks)
                .ThenInclude(t => t.Items)
                .FirstOrDefaultAsync(a => a.Code == req.ArkuszCode, ct);
            if (arkusz is null) return TypedResults.NotFound();
            if (!arkusz.IsFullExam) return Conflicts.Of(ProblemCodes.FullExamUnavailable, "Full mock exam is not available for this arkusz: some parts are not published yet");

            var session = ExamSession.Create(user.Id, arkusz, clock.GetUtcNow());
            db.ExamSessions.Add(session);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/exam-sessions/{session.Id}", SessionDto.Create(session, arkusz, []));
        });

        group.MapGet("/{id:guid}", async Task<Results<Ok<SessionDto>, NotFound>> (Guid id, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var loaded = await LoadAsync(db, id, user.Id, ct);
            return loaded is null ? TypedResults.NotFound() : TypedResults.Ok(SessionDto.Create(loaded.Value.Session, loaded.Value.Arkusz, loaded.Value.Attempts));
        });

        group.MapPost("/{id:guid}/parts", async Task<Results<Ok<ExamAttemptDto>, NotFound, ProblemHttpResult>> (
            Guid id, StartSessionPartRequest req, AppDbContext db, CurrentUser user, TimeProvider clock, CancellationToken ct) =>
        {
            var loaded = await LoadAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (session, arkusz, attempts) = loaded.Value;

            var part = arkusz.GetPart(req.Part);
            if (part is null) return TypedResults.NotFound();

            var existing = attempts.FirstOrDefault(a => a.PartKind == req.Part);
            if (existing is not null)
            {
                return existing.IsSubmitted
                    ? Conflicts.PartSubmitted("This part is already submitted")
                    : TypedResults.Ok(ExamAttemptDto.Create(existing, arkusz, part));
            }

            var summary = SessionScorer.Build(arkusz, attempts);
            if (summary.NextPart != req.Part) return Conflicts.Of(ProblemCodes.NextPartBlocked, $"Next part is {summary.NextPart}");

            var attempt = ExamAttempt.Create(user.Id, arkusz, part, clock.GetUtcNow(), session.Id);
            db.ExamAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(ExamAttemptDto.Create(attempt, arkusz, part));
        });

        // Already submitted standalone attempts of this arkusz that can count towards the mock exam (the latest one per part).
        group.MapGet("/{id:guid}/reusable", async Task<Results<Ok<List<ReusablePartDto>>, NotFound>> (Guid id, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var loaded = await LoadAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (session, _, _) = loaded.Value;
            var taken = await BlockingPartsAsync(db, session.Id, ct);

            var candidates = (await db.ExamAttempts
                    .Where(a => a.UserId == user.Id && a.ArkuszId == session.ArkuszId && a.SessionId == null && a.SubmittedAt != null)
                    .ToListAsync(ct))
                .Where(a => !taken.Contains(a.PartKind))
                .GroupBy(a => a.PartKind)
                .Select(g => g.OrderByDescending(a => a.SubmittedAt).First())
                .Select(a => new ReusablePartDto(a.PartKind, a.Id, a.SubmittedAt!.Value, a.Points, a.MaxPoints))
                .ToList();
            return TypedResults.Ok(candidates);
        });

        group.MapPost("/{id:guid}/parts/{partName}/adopt", async Task<Results<NoContent, NotFound, ProblemHttpResult>> (
            Guid id, string partName, AdoptPartRequest req, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            if (!Enum.TryParse<PartKind>(partName, ignoreCase: true, out var part) || !Enum.IsDefined(part)) return TypedResults.NotFound();
            var loaded = await LoadAsync(db, id, user.Id, ct);
            if (loaded is null) return TypedResults.NotFound();
            var (session, _, _) = loaded.Value;
            if ((await BlockingPartsAsync(db, session.Id, ct)).Contains(part)) return Conflicts.Conflict("This part is already in the mock exam");

            var attempt = await db.ExamAttempts.FirstOrDefaultAsync(a => a.Id == req.AttemptId && a.UserId == user.Id, ct);
            if (attempt is null) return TypedResults.NotFound();
            if (!attempt.CanBeAdoptedInto(session, part)) return Conflicts.Conflict("This attempt cannot be counted in this mock exam");

            // A part started in the session but still empty (no answers at all) is replaced by the adopted attempt
            var empty = await db.ExamAttempts.Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.SessionId == session.Id && a.PartKind == part && a.SubmittedAt == null, ct);
            if (empty is not null) db.ExamAttempts.Remove(empty);
            attempt.AdoptInto(session, part);

            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        });

        return app;
    }

    /// <summary>Session parts that cannot be replaced: submitted ones or started ones with at least one answer.</summary>
    private static async Task<HashSet<PartKind>> BlockingPartsAsync(AppDbContext db, Guid sessionId, CancellationToken ct) =>
        (await db.ExamAttempts.Include(a => a.Answers).Where(a => a.SessionId == sessionId).ToListAsync(ct))
            .Where(a => a.IsSubmitted || a.Answers.Any(x => !string.IsNullOrWhiteSpace(x.Value)))
            .Select(a => a.PartKind)
            .ToHashSet();

    private static async Task<(ExamSession Session, Arkusz Arkusz, List<ExamAttempt> Attempts)?> LoadAsync(AppDbContext db, Guid id, string userId, CancellationToken ct)
    {
        var session = await db.ExamSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (session is null) return null;

        var arkusz = await db.Arkusze
            .Include(a => a.Parts)
            .ThenInclude(p => p.Tasks)
            .ThenInclude(t => t.Items)
            .FirstOrDefaultAsync(a => a.Id == session.ArkuszId, ct);
        if (arkusz is null) return null;

        var attempts = await db.ExamAttempts.Where(a => a.SessionId == session.Id).ToListAsync(ct);
        return (session, arkusz, attempts);
    }
}
