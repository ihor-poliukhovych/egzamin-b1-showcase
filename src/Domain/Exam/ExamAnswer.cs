namespace B1.Domain.Exam;

public class ExamAnswer
{
    protected ExamAnswer() { }

    public ExamAnswer(Guid id, Guid attemptId, Guid itemId, string value, AnswerVerdict verdict, decimal points, DateTimeOffset updatedAt)
    {
        Id = id;
        AttemptId = attemptId;
        ItemId = itemId;
        Value = value;
        Verdict = verdict;
        Points = points;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid ItemId { get; set; }
    public string Value { get; set; } = string.Empty;
    public AnswerVerdict Verdict { get; set; }
    public decimal Points { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static ExamAnswer Create(Guid attemptId, Guid itemId, string value, DateTimeOffset updatedAt) =>
        new(Guid.NewGuid(), attemptId, itemId, value, AnswerVerdict.None, 0, updatedAt);
}
