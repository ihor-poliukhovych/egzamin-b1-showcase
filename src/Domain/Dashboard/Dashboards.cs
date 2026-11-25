using B1.Domain.Exam;
using B1.Domain.Groups;

namespace B1.Domain.Dashboard;

public enum TrendDirection
{
    None = 0,
    Up = 1,
    Flat = 2,
    Down = 3,
}

public class PartReadiness
{
    public PartReadiness(PartKind kind, PartResult last, List<decimal> trend, TrendDirection direction)
    {
        Kind = kind;
        Last = last;
        Trend = trend;
        Direction = direction;
    }

    public PartKind Kind { get; set; }
    public PartResult Last { get; set; }
    public List<decimal> Trend { get; set; }
    public TrendDirection Direction { get; set; }
}

public class HistoryEntry
{
    public HistoryEntry(Guid attemptId, Guid? sessionId, string arkuszCode, PartKind kind, decimal points, decimal maxPoints, DateTimeOffset submittedAt)
    {
        AttemptId = attemptId;
        SessionId = sessionId;
        ArkuszCode = arkuszCode;
        Kind = kind;
        Points = points;
        MaxPoints = maxPoints;
        SubmittedAt = submittedAt;
    }

    public Guid AttemptId { get; set; }
    public Guid? SessionId { get; set; }
    public string ArkuszCode { get; set; }
    public PartKind Kind { get; set; }
    public decimal Points { get; set; }
    public decimal MaxPoints { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }

    public decimal Percent => MaxPoints == 0 ? 0 : Math.Round(Points / MaxPoints * 100m, 1);
}

public class StudentReadiness
{
    public const int TrendLength = 5;
    public const decimal TrendTolerance = 2m;

    public static List<PartReadiness> Build(IReadOnlyCollection<ExamAttempt> attempts)
    {
        var result = new List<PartReadiness>();
        foreach (var kind in SessionScorer.AllParts)
        {
            var submitted = attempts
                .Where(a => a.PartKind == kind && a.IsSubmitted)
                .OrderBy(a => a.SubmittedAt)
                .ToList();

            var last = submitted.LastOrDefault();
            var part = last is null
                ? new PartResult(kind, PartStatus.NotStarted, null, kind == PartKind.Mowienie ? SelfAssessment.SpeakingMax : SelfAssessment.WritingMax, null, null)
                : new PartResult(kind, PartStatus.Submitted, last.Points, last.MaxPoints, last.Id, last.SubmittedAt);

            var trend = submitted
                .TakeLast(TrendLength)
                .Select(a => a.MaxPoints == 0 ? 0m : Math.Round(a.Points / a.MaxPoints * 100m, 1))
                .ToList();
            result.Add(new PartReadiness(kind, part, trend, DirectionOf(trend)));
        }

        return result;
    }

    public static TrendDirection DirectionOf(IReadOnlyList<decimal> trend)
    {
        if (trend.Count < 2) return TrendDirection.None;
        var delta = trend[^1] - trend[0];
        return delta > TrendTolerance ? TrendDirection.Up : delta < -TrendTolerance ? TrendDirection.Down : TrendDirection.Flat;
    }

    public static List<HistoryEntry> History(IReadOnlyCollection<ExamAttempt> attempts, IReadOnlyDictionary<Guid, string> arkuszCodes) => attempts
        .Where(a => a.IsSubmitted)
        .OrderByDescending(a => a.SubmittedAt)
        .Select(a => new HistoryEntry(a.Id, a.SessionId, arkuszCodes.GetValueOrDefault(a.ArkuszId, "?"), a.PartKind, a.Points, a.MaxPoints, a.SubmittedAt!.Value))
        .ToList();

    /// <summary>Best % for each arkusz x part pair; coverage = how many pairs have been completed at least once.</summary>
    public static Dictionary<string, Dictionary<PartKind, decimal>> BestMatrix(IReadOnlyCollection<ExamAttempt> attempts, IReadOnlyDictionary<Guid, string> arkuszCodes)
    {
        var matrix = new Dictionary<string, Dictionary<PartKind, decimal>>();
        foreach (var a in attempts.Where(x => x.IsSubmitted && x.MaxPoints > 0))
        {
            var code = arkuszCodes.GetValueOrDefault(a.ArkuszId, "?");
            var row = matrix.TryGetValue(code, out var existing) ? existing : matrix[code] = [];
            var pct = Math.Round(a.Points / a.MaxPoints * 100m, 1);
            row[a.PartKind] = row.TryGetValue(a.PartKind, out var best) ? Math.Max(best, pct) : pct;
        }

        return matrix;
    }

    /// <summary>Arkusz for the "Start mock exam" button: the first one with no completed parts yet.</summary>
    public static string? SuggestNext(IEnumerable<string> publishedCodes, IReadOnlyCollection<ExamAttempt> attempts, IReadOnlyDictionary<Guid, string> arkuszCodes)
    {
        var done = attempts.Where(a => a.IsSubmitted).Select(a => arkuszCodes.GetValueOrDefault(a.ArkuszId, "?")).ToHashSet();
        // newer sets are closer to the current exam format, so suggest the newest of the ones not yet done
        var codes = publishedCodes.OrderByDescending(c => c).ToList();
        return codes.FirstOrDefault(c => !done.Contains(c)) ?? codes.FirstOrDefault();
    }

    public static bool IsInactive(DateTimeOffset? lastActivity, DateTimeOffset now, int days = 7) =>
        lastActivity is null || now - lastActivity.Value >= TimeSpan.FromDays(days);
}

public class GroupSummary
{
    public GroupSummary(Guid groupId, string name, int students, Dictionary<PartKind, int> belowByPart, List<string> inactiveStudents,
        Dictionary<PartKind, int>? passedByPart = null, List<StudentBrief>? members = null)
    {
        GroupId = groupId;
        Name = name;
        Students = students;
        BelowByPart = belowByPart;
        InactiveStudents = inactiveStudents;
        PassedByPart = passedByPart ?? [];
        Members = members ?? [];
    }

    public Guid GroupId { get; set; }
    public string Name { get; set; }
    public int Students { get; set; }
    public Dictionary<PartKind, int> BelowByPart { get; set; }
    public List<string> InactiveStudents { get; set; }

    /// <summary>How many students meet the threshold with their latest attempt at a part, for the group progress bars.</summary>
    public Dictionary<PartKind, int> PassedByPart { get; set; }

    /// <summary>Group students with their latest percentage for each part (null means the part has not been taken yet).</summary>
    public List<StudentBrief> Members { get; set; }

    public static GroupSummary Build(Guid groupId, string name, IReadOnlyCollection<StudentStatus> students, DateTimeOffset now)
    {
        var below = SessionScorer.AllParts.ToDictionary(k => k, k => students.Count(s => s.Parts.Any(p => p.Kind == k && p.IsBelowThreshold)));
        var inactive = students
            .Where(s => StudentReadiness.IsInactive(s.LastActivityAt, now))
            .Select(s => s.DisplayName)
            .ToList();
        var passed = SessionScorer.AllParts.ToDictionary(k => k, k => students.Count(s => s.Parts.Any(p => p.Kind == k && p.Percent is not null && !p.IsBelowThreshold)));
        var members = students
            .OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new StudentBrief(s.StudentId, s.DisplayName, s.LastActivityAt, StudentReadiness.IsInactive(s.LastActivityAt, now),
                SessionScorer.AllParts.ToDictionary(k => k, k => s.Parts.FirstOrDefault(p => p.Kind == k)?.Percent)))
            .ToList();
        return new GroupSummary(groupId, name, students.Count, below, inactive, passed, members);
    }
}

public record StudentBrief(string StudentId, string DisplayName, DateTimeOffset? LastActivityAt, bool IsInactive, Dictionary<PartKind, decimal?> Percent);
