namespace B1.Domain.Exam;

public class ExamAttempt
{
    protected ExamAttempt() { }

    public ExamAttempt(Guid id, string userId, Guid arkuszId, PartKind partKind, DateTimeOffset startedAt, DateTimeOffset? submittedAt,
        decimal points, decimal maxPoints, List<ExamAnswer> answers)
    {
        Id = id;
        UserId = userId;
        ArkuszId = arkuszId;
        PartKind = partKind;
        StartedAt = startedAt;
        SubmittedAt = submittedAt;
        Points = points;
        MaxPoints = maxPoints;
        Answers = answers;
    }

    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ArkuszId { get; set; }
    public PartKind PartKind { get; set; }
    public Guid? SessionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public decimal Points { get; set; }
    public decimal MaxPoints { get; set; }
    public List<ExamAnswer> Answers { get; set; } = [];
    public double AudioPositionSec { get; set; }
    public int? ChosenSet { get; set; }
    public string? AssessmentJson { get; set; }
    public string? ReviewJson { get; set; }

    public bool IsSubmitted => SubmittedAt is not null;

    /// <summary>A submitted standalone attempt can be counted toward a mock exam of the same arkusz and the same user.</summary>
    public bool CanBeAdoptedInto(ExamSession session, PartKind part) =>
        SessionId is null && IsSubmitted && PartKind == part && ArkuszId == session.ArkuszId && UserId == session.UserId;

    public bool AdoptInto(ExamSession session, PartKind part)
    {
        if (!CanBeAdoptedInto(session, part)) return false;
        SessionId = session.Id;
        return true;
    }

    public static ExamAttempt Create(string userId, Arkusz arkusz, ExamPart part, DateTimeOffset startedAt, Guid? sessionId = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required", nameof(userId));
        var attempt = new ExamAttempt(Guid.NewGuid(), userId, arkusz.Id, part.Kind, startedAt, null, 0, part.MaxPoints, []);
        attempt.SessionId = sessionId;
        return attempt;
    }

    public ExamAnswer SaveAnswer(Guid itemId, string value, DateTimeOffset now)
    {
        if (IsSubmitted) throw new InvalidOperationException("Attempt is already submitted");

        var answer = Answers.FirstOrDefault(a => a.ItemId == itemId);
        if (answer is null)
        {
            answer = ExamAnswer.Create(Id, itemId, value, now);
            Answers.Add(answer);
            return answer;
        }

        answer.Value = value;
        answer.UpdatedAt = now;
        return answer;
    }

    public void FinishSelfAssessed(decimal points, decimal maxPoints, DateTimeOffset now)
    {
        if (IsSubmitted) throw new InvalidOperationException("Attempt is already submitted");
        Points = points;
        MaxPoints = maxPoints;
        SubmittedAt = now;
    }

    public void SaveAudioPosition(double seconds)
    {
        if (IsSubmitted) throw new InvalidOperationException("Attempt is already submitted");
        if (double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        AudioPositionSec = Math.Max(0, seconds);
    }

    public ExamAnswer? FindAnswer(Guid itemId) => Answers.FirstOrDefault(a => a.ItemId == itemId);
}
