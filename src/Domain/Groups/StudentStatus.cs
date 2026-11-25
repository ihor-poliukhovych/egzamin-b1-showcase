using B1.Domain.Auth;
using B1.Domain.ErrorBank;
using B1.Domain.Exam;

namespace B1.Domain.Groups;

public record PatternBrief(string Title, int FallsCount);

public class StudentStatus
{
    public StudentStatus(string studentId, string displayName, string email, DateTimeOffset joinedAt, List<PartResult> parts, List<PatternBrief> topPatterns, DateTimeOffset? lastActivityAt)
    {
        StudentId = studentId;
        DisplayName = displayName;
        Email = email;
        JoinedAt = joinedAt;
        Parts = parts;
        TopPatterns = topPatterns;
        LastActivityAt = lastActivityAt;
    }

    public string StudentId { get; set; }
    public string DisplayName { get; set; }
    public string Email { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public List<PartResult> Parts { get; set; }
    public List<PatternBrief> TopPatterns { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }

    public static StudentStatus Build(User student, GroupMember member, IReadOnlyCollection<ExamAttempt> attempts,
        IReadOnlyCollection<ErrorPattern> patterns, IReadOnlyCollection<DateOnly> reviewDates)
    {
        var parts = SessionScorer.AllParts.Select(kind => LastResult(kind, attempts)).ToList();

        var top = patterns
            .Where(p => p.Status != PatternStatus.Archived && p.FallsCount > 0)
            .OrderByDescending(p => p.FallsCount)
            .ThenBy(p => p.Title)
            .Take(3)
            .Select(p => new PatternBrief(p.Title, p.FallsCount))
            .ToList();

        var activity = attempts.Select(a => a.SubmittedAt ?? a.StartedAt)
            .Concat(reviewDates.Select(d => new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)))
            .Select(d => (DateTimeOffset?)d)
            .Max();

        return new StudentStatus(student.UserKey, student.DisplayName, student.Email, member.JoinedAt, parts, top, activity);
    }

    private static PartResult LastResult(PartKind kind, IReadOnlyCollection<ExamAttempt> attempts)
    {
        var last = attempts
            .Where(a => a.PartKind == kind && a.IsSubmitted)
            .OrderByDescending(a => a.SubmittedAt)
            .FirstOrDefault();

        var max = kind switch
        {
            PartKind.Mowienie => SelfAssessment.SpeakingMax,
            _ => SelfAssessment.WritingMax,
        };

        return last is null
            ? new PartResult(kind, PartStatus.NotStarted, null, max, null, null)
            : new PartResult(kind, PartStatus.Submitted, last.Points, last.MaxPoints, last.Id, last.SubmittedAt);
    }
}
