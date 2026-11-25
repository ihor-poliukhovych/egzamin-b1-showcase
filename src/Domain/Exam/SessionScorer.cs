namespace B1.Domain.Exam;

public enum PartStatus
{
    None = 0,
    NotStarted = 1,
    InProgress = 2,
    Submitted = 3,
    NotAvailable = 4,
}

public enum SessionVerdict
{
    None = 0,
    InProgress = 1,
    Incomplete = 2,
    Passed = 3,
    Failed = 4,
}

public class PartResult
{
    public PartResult(PartKind kind, PartStatus status, decimal? points, decimal maxPoints, Guid? attemptId, DateTimeOffset? submittedAt)
    {
        Kind = kind;
        Status = status;
        Points = points;
        MaxPoints = maxPoints;
        AttemptId = attemptId;
        SubmittedAt = submittedAt;
    }

    public PartKind Kind { get; set; }
    public PartStatus Status { get; set; }
    public decimal? Points { get; set; }
    public decimal MaxPoints { get; set; }
    public Guid? AttemptId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    public decimal? Percent => Points is null || MaxPoints == 0 ? null : Math.Round(Points.Value / MaxPoints * 100m, 1);
    public bool IsBelowThreshold => Percent is not null && Percent < ExamGrader.PassThresholdPercent;
}

public class SessionSummary
{
    public SessionSummary(List<PartResult> parts, SessionVerdict verdict, PartKind? nextPart)
    {
        Parts = parts;
        Verdict = verdict;
        NextPart = nextPart;
    }

    public List<PartResult> Parts { get; set; }
    public SessionVerdict Verdict { get; set; }
    public PartKind? NextPart { get; set; }
}

public static class SessionScorer
{
    public const decimal DefaultWritingMax = 30m;
    public const decimal DefaultSpeakingMax = 40m;

    public static readonly PartKind[] AllParts = [PartKind.Sluchanie, PartKind.Czytanie, PartKind.Gramatyka, PartKind.Pisanie, PartKind.Mowienie];
    public static readonly PartKind[] Implemented = [PartKind.Sluchanie, PartKind.Czytanie, PartKind.Gramatyka, PartKind.Pisanie, PartKind.Mowienie];

    public static SessionSummary Build(Arkusz arkusz, IReadOnlyCollection<ExamAttempt> attempts)
    {
        var kinds = KindsOf(arkusz);
        var parts = kinds.Select(kind => BuildPart(arkusz, attempts, kind)).ToList();

        var next = kinds
            .Where(Implemented.Contains)
            .Where(kind => parts.First(p => p.Kind == kind).Status != PartStatus.Submitted)
            .Select(kind => (PartKind?)kind)
            .FirstOrDefault();

        return new SessionSummary(parts, DecideVerdict(parts), next);
    }

    /// <summary>Which parts exist in the arkusz (some levels have no Mówienie); an empty arkusz means all five.</summary>
    public static PartKind[] KindsOf(Arkusz arkusz) =>
        arkusz.Parts.Count == 0 ? AllParts : AllParts.Where(k => arkusz.GetPart(k) is not null).ToArray();

    public static SessionVerdict DecideVerdict(IReadOnlyCollection<PartResult> parts)
    {
        if (parts.Any(p => p.IsBelowThreshold)) return SessionVerdict.Failed;
        if (parts.All(p => p.Points is not null)) return SessionVerdict.Passed;
        if (parts.Where(p => Implemented.Contains(p.Kind)).All(p => p.Status == PartStatus.Submitted)) return SessionVerdict.Incomplete;
        return SessionVerdict.InProgress;
    }

    private static PartResult BuildPart(Arkusz arkusz, IReadOnlyCollection<ExamAttempt> attempts, PartKind kind)
    {
        var fallbackMax = kind == PartKind.Mowienie ? DefaultSpeakingMax : DefaultWritingMax;
        var max = arkusz.GetPart(kind)?.MaxPoints ?? fallbackMax;

        if (!Implemented.Contains(kind)) return new PartResult(kind, PartStatus.NotAvailable, null, max, null, null);

        var attempt = attempts.Where(a => a.PartKind == kind).OrderByDescending(a => a.StartedAt).FirstOrDefault();
        if (attempt is null) return new PartResult(kind, PartStatus.NotStarted, null, max, null, null);
        if (!attempt.IsSubmitted) return new PartResult(kind, PartStatus.InProgress, null, max, attempt.Id, null);
        return new PartResult(kind, PartStatus.Submitted, attempt.Points, attempt.MaxPoints, attempt.Id, attempt.SubmittedAt);
    }
}
