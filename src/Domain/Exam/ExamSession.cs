namespace B1.Domain.Exam;

public class ExamSession
{
    protected ExamSession() { }

    public ExamSession(Guid id, string userId, Guid arkuszId, DateTimeOffset startedAt)
    {
        Id = id;
        UserId = userId;
        ArkuszId = arkuszId;
        StartedAt = startedAt;
    }

    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ArkuszId { get; set; }
    public DateTimeOffset StartedAt { get; set; }

    public static ExamSession Create(string userId, Arkusz arkusz, DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required", nameof(userId));
        return new ExamSession(Guid.NewGuid(), userId, arkusz.Id, startedAt);
    }
}
