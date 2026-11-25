using B1.Domain.ErrorBank;

namespace B1.Api;

public record PatternDto(
    Guid Id,
    string? Code,
    string Title,
    string WrongExample,
    string CorrectForm,
    string Rule,
    string? ContrastRuUa,
    PatternCategory Category,
    string? GroupName,
    PatternStatus Status,
    int FallsCount,
    int CleanStreak,
    DateOnly? NextDueAt,
    DateOnly? LastFailDate,
    DateOnly? LastCleanPassDate,
    DateTimeOffset CreatedAt)
{
    public static PatternDto From(ErrorPattern p) => new(
        p.Id, p.Code, p.Title, p.WrongExample, p.CorrectForm, p.Rule, p.ContrastRuUa, p.Category, p.GroupName, p.Status,
        p.FallsCount, p.CleanStreak, p.NextDueAt, p.LastFailDate, p.LastCleanPassDate, p.CreatedAt);
}

public record PatternWriteRequest(
    string Title,
    string WrongExample,
    string CorrectForm,
    string Rule,
    PatternCategory Category,
    string? ContrastRuUa = null,
    string? Code = null);

public record AttemptRequest(
    Guid PatternId,
    AttemptResult Result,
    AttemptSource Source = AttemptSource.Blitz,
    string? Evidence = null,
    bool CountsTowardStreak = true,
    DateOnly? Date = null);

public record AttemptResponse(Guid AttemptId, PatternDto Pattern, TodayDto Today);

public record TodayItemDto(PatternDto Pattern, AttemptResult? Result, int Order);

public record TodayDto(Guid SessionId, DateOnly Date, bool IsComplete, int Answered, int Total, IReadOnlyList<TodayItemDto> Items);
