namespace B1.Domain.Exam;

public class ExamPart
{
    protected ExamPart() { }

    public ExamPart(Guid id, Guid arkuszId, PartKind kind, int timeLimitMinutes, List<ExamTask> tasks)
    {
        Id = id;
        ArkuszId = arkuszId;
        Kind = kind;
        TimeLimitMinutes = timeLimitMinutes;
        Tasks = tasks;
    }

    public Guid Id { get; set; }
    public Guid ArkuszId { get; set; }
    public PartKind Kind { get; set; }
    public int TimeLimitMinutes { get; set; }
    public string? AudioKey { get; set; }

    /// <summary>Maximum score of the part as given by the arkusz itself (Pisanie, Mówienie and parts with a non-standard scale); otherwise the sum of task points.</summary>
    public decimal? MaxPointsOverride { get; set; }
    public List<ExamTask> Tasks { get; set; } = [];

    public decimal MaxPoints => MaxPointsOverride ?? Kind switch
    {
        PartKind.Pisanie => SelfAssessment.WritingMax,
        PartKind.Mowienie => SelfAssessment.SpeakingMax,
        _ => Tasks.Sum(t => t.MaxPoints),
    };

    public static ExamPart Create(Guid arkuszId, PartKind kind, int timeLimitMinutes, List<ExamTask> tasks)
    {
        if (kind == PartKind.None) throw new ArgumentException("kind is required", nameof(kind));
        return new ExamPart(Guid.NewGuid(), arkuszId, kind, timeLimitMinutes, tasks);
    }

    public ExamItem? FindItem(Guid itemId) => Tasks
        .SelectMany(t => t.Items)
        .FirstOrDefault(i => i.Id == itemId);

    public ExamTask? FindTaskByItem(Guid itemId) => Tasks
        .FirstOrDefault(t => t.Items.Any(i => i.Id == itemId));
}
