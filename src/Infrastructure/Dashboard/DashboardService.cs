using B1.Domain.Dashboard;
using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using B1.Domain.Groups;
using B1.Infrastructure.Groups;
using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B1.Infrastructure.Dashboard;

public class StudentDashboard
{
    public StudentDashboard(List<PartReadiness> readiness, List<HistoryEntry> history, Dictionary<string, Dictionary<PartKind, decimal>> matrix,
        List<string> arkuszCodes, List<string> fullArkuszCodes, int coveredPairs, int totalPairs, List<PatternBrief> topPatterns, List<MyGroup> groups, string? suggestedArkusz)
    {
        Readiness = readiness;
        History = history;
        Matrix = matrix;
        ArkuszCodes = arkuszCodes;
        FullArkuszCodes = fullArkuszCodes;
        CoveredPairs = coveredPairs;
        TotalPairs = totalPairs;
        TopPatterns = topPatterns;
        Groups = groups;
        SuggestedArkusz = suggestedArkusz;
    }

    public List<PartReadiness> Readiness { get; set; }
    public List<HistoryEntry> History { get; set; }
    public Dictionary<string, Dictionary<PartKind, decimal>> Matrix { get; set; }
    public List<string> ArkuszCodes { get; set; }
    public List<string> FullArkuszCodes { get; set; }
    public int CoveredPairs { get; set; }
    public int TotalPairs { get; set; }
    public List<PatternBrief> TopPatterns { get; set; }
    public List<MyGroup> Groups { get; set; }
    public string? SuggestedArkusz { get; set; }
}

public class DashboardService
{
    private readonly AppDbContext _appDbContext;
    private readonly GroupService _groupService;
    private readonly TimeProvider _timeProvider;

    public DashboardService(AppDbContext appDbContext, GroupService groupService, TimeProvider timeProvider)
    {
        _appDbContext = appDbContext;
        _groupService = groupService;
        _timeProvider = timeProvider;
    }

    public async Task<StudentDashboard> GetStudentAsync(string userId, string level = "B1", string variant = "dorosli", CancellationToken ct = default)
    {
        var arkusze = await _appDbContext.Arkusze.Include(a => a.Parts).Where(a => a.Level == level && a.Variant == variant).ToListAsync(ct);
        var ids = arkusze.Select(a => a.Id).ToList();
        var attempts = await _appDbContext.ExamAttempts.Where(a => a.UserId == userId && ids.Contains(a.ArkuszId)).ToListAsync(ct);
        var codes = arkusze.ToDictionary(a => a.Id, a => a.Code);
        var published = arkusze.Select(a => a.Code).OrderBy(c => c).ToList();
        var full = arkusze.Where(a => a.IsFullExam).Select(a => a.Code).OrderBy(c => c).ToList();

        var patterns = await _appDbContext.ErrorPatterns.Where(p => p.UserId == userId).ToListAsync(ct);
        var top = patterns
            .Where(p => p.Status != PatternStatus.Archived && p.FallsCount > 0)
            .OrderByDescending(p => p.FallsCount)
            .ThenBy(p => p.Title)
            .Take(5)
            .Select(p => new PatternBrief(p.Title, p.FallsCount))
            .ToList();

        var matrix = StudentReadiness.BestMatrix(attempts, codes);
        var covered = matrix.Values.Sum(r => r.Count);
        return new StudentDashboard(
            StudentReadiness.Build(attempts),
            StudentReadiness.History(attempts, codes).Take(30).ToList(),
            matrix, published, full, covered, arkusze.Sum(a => a.Parts.Count),
            top, await _groupService.MyGroupsAsync(userId, ct),
            StudentReadiness.SuggestNext(full, attempts, codes));
    }

    /// <summary>Student card for the teacher: only their own group and only a member who gave consent.</summary>
    public async Task<StudentDashboard?> GetForTeacherAsync(Guid groupId, string teacherId, string studentId, string level = "B1", string variant = "dorosli", CancellationToken ct = default)
    {
        var students = await _groupService.GetStudentsAsync(groupId, teacherId, ct);
        if (students is null || students.All(s => s.StudentId != studentId)) return null;

        // The student's membership in other teachers' groups is none of their business: keep only this teacher's groups.
        var dashboard = await GetStudentAsync(studentId, level, variant, ct);
        var ownGroupIds = (await _groupService.ListAsync(teacherId, includeArchived: true, ct)).Select(g => g.Id).ToHashSet();
        dashboard.Groups = dashboard.Groups.Where(g => ownGroupIds.Contains(g.GroupId)).ToList();
        return dashboard;
    }

    public async Task<List<GroupSummary>> GetTeacherSummaryAsync(string teacherId, CancellationToken ct = default, string level = "B1", string variant = "dorosli")
    {
        var now = _timeProvider.GetUtcNow();
        var result = new List<GroupSummary>();
        foreach (var group in await _groupService.ListAsync(teacherId, includeArchived: false, ct))
        {
            var students = await _groupService.GetStudentsAsync(group.Id, teacherId, ct, level, variant) ?? [];
            result.Add(GroupSummary.Build(group.Id, group.Name, students, now));
        }

        return result;
    }
}
