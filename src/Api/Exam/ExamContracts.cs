using System.Text.Json;
using B1.Domain.Exam;

namespace B1.Api.Exam;

public record PartSummaryDto(PartKind Kind, int TimeLimitMinutes, int TaskCount, decimal MaxPoints)
{
    public static PartSummaryDto Create(ExamPart p) => new(p.Kind, p.TimeLimitMinutes, p.Tasks.Count, p.MaxPoints);
}

public record ArkuszSummaryDto(Guid Id, string Code, string Title, ArkuszSource Source, decimal MaxPoints, IReadOnlyList<PartSummaryDto> Parts, string Level, string Variant, bool FullExam)
{
    public static ArkuszSummaryDto Create(Arkusz a) => new(
        a.Id, a.Code, a.Title, a.Source, a.MaxPoints, a.Parts.OrderBy(p => p.Kind).Select(PartSummaryDto.Create).ToList(), a.Level, a.Variant, a.IsFullExam);
}

/// <summary>Mowienie task 3 dialog for the client: who the examiner plays, who starts and how many lines the examiner has.</summary>
public record DialogInfoDto(string Role, bool ExaminerOpens, int Turns);

public record ExamItemDto(Guid Id, int Number, string? Prompt, string? Hint, IReadOnlyList<string> Options, int TargetWords, DialogInfoDto? Dialog)
{
    public static ExamItemDto Create(ExamItem i) => new(i.Id, i.Number, i.Prompt, i.Hint, i.Options, i.TargetWords, DialogOf(i));

    private static DialogInfoDto? DialogOf(ExamItem i)
    {
        if (i.Hint != "sytuacja") return null;
        var template = Dialogs.For(i.Prompt);
        return new DialogInfoDto(template.Role, template.ExaminerOpens, template.Beats.Count);
    }
}

public record ExamTaskDto(
    Guid Id,
    int Number,
    string RomanNumber,
    ExamTaskType Type,
    string Instruction,
    decimal PointsPerItem,
    decimal MaxPoints,
    string? Text,
    string? BasedOn,
    string? Example,
    IReadOnlyList<string> WordBank,
    IReadOnlyList<ExamItemDto> Items,
    string? Image = null)
{
    public static ExamTaskDto Create(ExamTask t) => new(
        t.Id, t.Number, t.RomanNumber, t.Type, t.Instruction, t.PointsPerItem, t.MaxPoints, t.Text, t.BasedOn, t.Example, t.WordBank,
        t.Items.OrderBy(i => i.Number).Select(ExamItemDto.Create).ToList(), t.Image);
}

public record ArkuszDto(Guid Id, string Code, string Title, ArkuszSource Source, PartKind Part, int TimeLimitMinutes, decimal MaxPoints, IReadOnlyList<ExamTaskDto> Tasks, string? AudioKey)
{
    public static ArkuszDto Create(Arkusz a, ExamPart p) => new(
        a.Id, a.Code, a.Title, a.Source, p.Kind, p.TimeLimitMinutes, p.MaxPoints,
        p.Tasks.OrderBy(t => t.Number).Select(ExamTaskDto.Create).ToList(), p.AudioKey);
}

public record ExamAnswerDto(Guid ItemId, string Value)
{
    public static ExamAnswerDto Create(ExamAnswer a) => new(a.ItemId, a.Value);
}

public record ItemResultDto(
    Guid ItemId,
    int TaskNumber,
    string TaskRomanNumber,
    int Number,
    string? Prompt,
    string? Hint,
    string Value,
    IReadOnlyList<string> AnswerKey,
    string? Explanation,
    AnswerVerdict Verdict,
    decimal Points,
    decimal MaxPoints);

public record TaskScoreDto(int Number, string RomanNumber, decimal Points, decimal MaxPoints)
{
    public static TaskScoreDto Create(TaskScore s) => new(s.Number, s.RomanNumber, s.Points, s.MaxPoints);
}

public record TranscriptDto(int TaskNumber, string TaskRomanNumber, string Text);

public record ExamResultDto(decimal Points, decimal MaxPoints, decimal Percent, bool IsPassed, decimal PassThresholdPercent,
    IReadOnlyList<TaskScoreDto> Tasks, IReadOnlyList<ItemResultDto> Items, IReadOnlyList<TranscriptDto> Transcripts)
{
    public static ExamResultDto Create(ExamAttempt attempt, ExamPart part)
    {
        var score = ExamGrader.Score(attempt, part);
        var items = part.Tasks
            .OrderBy(t => t.Number)
            .SelectMany(t => t.Items.OrderBy(i => i.Number).Select(i => (Task: t, Item: i)))
            .Select(x =>
            {
                var answer = attempt.FindAnswer(x.Item.Id);
                return new ItemResultDto(x.Item.Id, x.Task.Number, x.Task.RomanNumber, x.Item.Number, x.Item.Prompt, x.Item.Hint,
                    answer?.Value ?? string.Empty, x.Item.AnswerKey, x.Item.Explanation,
                    answer?.Verdict ?? AnswerVerdict.Wrong, answer?.Points ?? 0m, x.Task.PointsOf(x.Item));
            })
            .ToList();

        return new ExamResultDto(score.Points, score.MaxPoints, score.Percent, score.IsPassed, ExamGrader.PassThresholdPercent,
            score.Tasks.Select(TaskScoreDto.Create).ToList(), items,
            part.Tasks.Where(t => !string.IsNullOrEmpty(t.Transcript)).OrderBy(t => t.Number).Select(t => new TranscriptDto(t.Number, t.RomanNumber, t.Transcript!)).ToList());
    }
}

public record ExamAttemptDto(
    Guid Id,
    Guid? SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset? SubmittedAt,
    ArkuszDto Arkusz,
    IReadOnlyList<ExamAnswerDto> Answers,
    ExamResultDto? Result,
    double AudioPositionSec,
    int? ChosenSet,
    JsonElement? Assessment,
    JsonElement? Review,
    decimal Points)
{
    public static ExamAttemptDto Create(ExamAttempt attempt, Arkusz arkusz, ExamPart part) => new(
        attempt.Id, attempt.SessionId, attempt.StartedAt, attempt.SubmittedAt, ArkuszDto.Create(arkusz, part),
        attempt.Answers.Select(ExamAnswerDto.Create).ToList(),
        attempt.IsSubmitted && part.Kind is not (PartKind.Pisanie or PartKind.Mowienie) ? ExamResultDto.Create(attempt, part) : null,
        attempt.AudioPositionSec,
        attempt.ChosenSet, ParseJson(attempt.AssessmentJson), ParseJson(attempt.ReviewJson), attempt.Points);

    private static JsonElement? ParseJson(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonDocument.Parse(json).RootElement.Clone();
}

public record ExamAttemptSummaryDto(Guid Id, PartKind Part, string ArkuszCode, string ArkuszTitle, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt,
    decimal Points, decimal MaxPoints, int Answered, int Total)
{
    public static ExamAttemptSummaryDto Create(ExamAttempt attempt, Arkusz arkusz) => new(
        attempt.Id, attempt.PartKind, arkusz.Code, arkusz.Title, attempt.StartedAt, attempt.SubmittedAt, attempt.Points, attempt.MaxPoints,
        attempt.Answers.Count(a => !string.IsNullOrWhiteSpace(a.Value)), arkusz.GetPart(attempt.PartKind)?.Tasks.Sum(t => t.Items.Count) ?? 0);
}

public record AudioPositionRequest(double Seconds);

public record PartResultDto(PartKind Kind, PartStatus Status, decimal? Points, decimal MaxPoints, decimal? Percent, bool IsBelowThreshold, Guid? AttemptId, DateTimeOffset? SubmittedAt,
    int TimeLimitMinutes)
{
    public static PartResultDto Create(PartResult p, Arkusz arkusz) => new(p.Kind, p.Status, p.Points, p.MaxPoints, p.Percent, p.IsBelowThreshold, p.AttemptId, p.SubmittedAt,
        arkusz.GetPart(p.Kind)?.TimeLimitMinutes ?? 0);
}

public record SessionDto(Guid Id, string ArkuszCode, string ArkuszTitle, DateTimeOffset StartedAt, SessionVerdict Verdict, PartKind? NextPart,
    decimal PassThresholdPercent, IReadOnlyList<PartResultDto> Parts)
{
    public static SessionDto Create(ExamSession session, Arkusz arkusz, IReadOnlyCollection<ExamAttempt> attempts)
    {
        var summary = SessionScorer.Build(arkusz, attempts);
        return new SessionDto(session.Id, arkusz.Code, arkusz.Title, session.StartedAt, summary.Verdict, summary.NextPart,
            ExamGrader.PassThresholdPercent, summary.Parts.Select(p => PartResultDto.Create(p, arkusz)).ToList());
    }
}

public record ChooseSetRequest(int Set);

public record DetailsRequest(JsonElement Assessment);

public record WordCountRequest(string? Text, int Target);

public record WordCountDto(int Count, int Min, int Max, WordCountStatus Status);

public record StartSessionRequest(string ArkuszCode);

public record StartSessionPartRequest(PartKind Part);

public record StartAttemptRequest(string ArkuszCode, PartKind Part = PartKind.Gramatyka);

public record SaveAnswersRequest(IReadOnlyList<ExamAnswerDto> Answers);

