namespace B1.Domain.Exam;

public static class ExamGrader
{
    public const decimal PassThresholdPercent = 50m;

    public static AnswerVerdict GradeAnswer(ExamTask task, ExamItem item, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return AnswerVerdict.Wrong;
        if (AnswerNormalizer.Matches(value, item.AnswerKey)) return AnswerVerdict.Correct;
        return task.IsFreeText ? AnswerVerdict.NeedsReview : AnswerVerdict.Wrong;
    }

    public static decimal PointsFor(AnswerVerdict verdict, decimal pointsPerItem) =>
        verdict is AnswerVerdict.Correct or AnswerVerdict.Accepted ? pointsPerItem : 0m;

    public static void Submit(ExamAttempt attempt, ExamPart part, DateTimeOffset now)
    {
        if (attempt.IsSubmitted) throw new InvalidOperationException("Attempt is already submitted");
        if (attempt.ArkuszId != part.ArkuszId || attempt.PartKind != part.Kind) throw new InvalidOperationException("Attempt belongs to another part");

        foreach (var task in part.Tasks)
        {
            foreach (var item in task.Items)
            {
                var answer = attempt.FindAnswer(item.Id) ?? attempt.SaveAnswer(item.Id, string.Empty, now);
                answer.Verdict = GradeAnswer(task, item, answer.Value);
                answer.Points = PointsFor(answer.Verdict, task.PointsOf(item));
            }
        }

        attempt.SubmittedAt = now;
        attempt.MaxPoints = part.MaxPoints;
        attempt.Points = attempt.Answers.Sum(a => a.Points);
    }

    /// <summary>The student accepts their own free-text answer that the key did not match.</summary>
    /// <returns>false when the answer is not waiting for review (only <see cref="AnswerVerdict.NeedsReview"/> can be accepted).</returns>
    public static bool Accept(ExamAttempt attempt, ExamPart part, Guid itemId)
    {
        if (!attempt.IsSubmitted) throw new InvalidOperationException("Attempt is not submitted yet");

        var task = part.FindTaskByItem(itemId) ?? throw new InvalidOperationException("Item not found in arkusz");
        var answer = attempt.FindAnswer(itemId);
        if (answer is not { Verdict: AnswerVerdict.NeedsReview }) return false;

        answer.Verdict = AnswerVerdict.Accepted;
        answer.Points = PointsFor(answer.Verdict, task.PointsOf(task.Items.First(i => i.Id == itemId)));
        attempt.Points = attempt.Answers.Sum(a => a.Points);
        return true;
    }

    public static ExamScore Score(ExamAttempt attempt, ExamPart part)
    {
        var taskScores = part.Tasks
            .OrderBy(t => t.Number)
            .Select(t => new TaskScore(t.Number, t.RomanNumber, t.Items.Sum(i => attempt.FindAnswer(i.Id)?.Points ?? 0m), t.MaxPoints))
            .ToList();

        var points = taskScores.Sum(t => t.Points);
        var maxPoints = taskScores.Sum(t => t.MaxPoints);
        return new ExamScore(points, maxPoints, taskScores);
    }
}

public class ExamScore
{
    public ExamScore(decimal points, decimal maxPoints, List<TaskScore> tasks)
    {
        Points = points;
        MaxPoints = maxPoints;
        Tasks = tasks;
    }

    public decimal Points { get; set; }
    public decimal MaxPoints { get; set; }
    public List<TaskScore> Tasks { get; set; }

    public decimal Percent => MaxPoints == 0 ? 0 : Math.Round(Points / MaxPoints * 100m, 1);
    public bool IsPassed => Percent >= ExamGrader.PassThresholdPercent;
}

public class TaskScore
{
    public TaskScore(int number, string romanNumber, decimal points, decimal maxPoints)
    {
        Number = number;
        RomanNumber = romanNumber;
        Points = points;
        MaxPoints = maxPoints;
    }

    public int Number { get; set; }
    public string RomanNumber { get; set; }
    public decimal Points { get; set; }
    public decimal MaxPoints { get; set; }
}
