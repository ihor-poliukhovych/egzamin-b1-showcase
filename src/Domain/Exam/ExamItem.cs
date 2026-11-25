namespace B1.Domain.Exam;

public class ExamItem
{
    protected ExamItem() { }

    public ExamItem(Guid id, Guid taskId, int number, string? prompt, string? hint, List<string> options, List<string> answerKey, string? explanation, int targetWords = 0)
    {
        Id = id;
        TaskId = taskId;
        Number = number;
        Prompt = prompt;
        Hint = hint;
        Options = options;
        AnswerKey = answerKey;
        Explanation = explanation;
        TargetWords = targetWords;
    }

    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public int Number { get; set; }
    public string? Prompt { get; set; }
    public string? Hint { get; set; }
    public List<string> Options { get; set; } = [];
    public List<string> AnswerKey { get; set; } = [];
    public string? Explanation { get; set; }
    public int TargetWords { get; set; }

    /// <summary>Points for the item when they differ from the task points (for example, 0.5 per sub-item of an open question).</summary>
    public decimal? PointsOverride { get; set; }

    public static ExamItem Create(Guid taskId, int number, string? prompt, string? hint, List<string> options, List<string> answerKey, string? explanation, int targetWords = 0)
    {
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        return new ExamItem(Guid.NewGuid(), taskId, number, prompt, hint, options, answerKey, explanation, targetWords);
    }
}
