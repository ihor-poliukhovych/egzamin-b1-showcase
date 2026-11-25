using B1.Domain.ErrorBank;
using B1.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace B1.Api.Endpoints;

public static class PatternEndpoints
{
    public static IEndpointRouteBuilder MapPatterns(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/patterns").WithTags("ErrorPatterns");

        group.MapGet("/", async (AppDbContext db, CurrentUser user, string? status, CancellationToken ct) =>
        {
            var query = db.ErrorPatterns.Where(p => p.UserId == user.Id);
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<PatternStatus>(status, ignoreCase: true, out var parsed))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Use watch | active | archived"] });
                query = query.Where(p => p.Status == parsed);
            }
            var list = await query
                .OrderByDescending(p => p.FallsCount)
                .ThenBy(p => p.Code)
                .ToListAsync(ct);
            return Results.Ok(list.Select(PatternDto.From));
        }).Produces<IEnumerable<PatternDto>>();

        group.MapGet("/{id:guid}", async Task<Results<Ok<PatternDto>, NotFound>> (Guid id, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var p = await db.ErrorPatterns.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id, ct);
            return p is null ? TypedResults.NotFound() : TypedResults.Ok(PatternDto.From(p));
        });

        group.MapPost("/", async Task<Results<Created<PatternDto>, ValidationProblem>> (PatternWriteRequest req, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            if (Validate(req) is { } errors) return TypedResults.ValidationProblem(errors);
            var p = new ErrorPattern(user.Id, req.Title, req.WrongExample, req.CorrectForm, req.Rule, req.Category, req.ContrastRuUa, req.Code);
            db.ErrorPatterns.Add(p);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/patterns/{p.Id}", PatternDto.From(p));
        });

        group.MapPut("/{id:guid}", async Task<Results<Ok<PatternDto>, NotFound, ValidationProblem>> (Guid id, PatternWriteRequest req, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            if (Validate(req) is { } errors) return TypedResults.ValidationProblem(errors);
            var p = await db.ErrorPatterns.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id, ct);
            if (p is null) return TypedResults.NotFound();
            p.Update(req.Title, req.WrongExample, req.CorrectForm, req.Rule, req.Category, req.ContrastRuUa, req.Code);
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok(PatternDto.From(p));
        });

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, AppDbContext db, CurrentUser user, CancellationToken ct) =>
        {
            var p = await db.ErrorPatterns.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id, ct);
            if (p is null) return TypedResults.NotFound();
            db.ErrorPatterns.Remove(p);
            await db.SaveChangesAsync(ct);
            return TypedResults.NoContent();
        });

        return app;
    }

    public const int MaxTitleLength = 512;

    private static Dictionary<string, string[]>? Validate(PatternWriteRequest req)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Title)) errors["title"] = ["Title is required"];
        else if (req.Title.Length > MaxTitleLength) errors["title"] = [$"Title must be at most {MaxTitleLength} characters"];
        if (string.IsNullOrWhiteSpace(req.CorrectForm)) errors["correctForm"] = ["CorrectForm is required"];
        if (req.WrongExample is null) errors["wrongExample"] = ["WrongExample is required (may be empty string)"];
        if (req.Rule is null) errors["rule"] = ["Rule is required (may be empty string)"];
        if (req.Code is { Length: > 16 }) errors["code"] = ["Code must be at most 16 characters"];
        return errors.Count == 0 ? null : errors;
    }
}
