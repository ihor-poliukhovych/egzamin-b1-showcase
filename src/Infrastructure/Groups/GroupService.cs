using B1.Domain.Auth;
using B1.Domain.Groups;
using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B1.Infrastructure.Groups;

public enum JoinOutcome
{
    Joined = 0,
    AlreadyMember = 1,
    NotFound = 2,
    Revoked = 3,
    Expired = 4,
    Exhausted = 5,
    Archived = 6,
    ConsentRequired = 7,
    OwnGroup = 8,
}

public record InvitePreview(JoinOutcome State, Guid? GroupId, string? GroupName, string? TeacherName);

public record MyGroup(Guid GroupId, string Name, string TeacherName, DateTimeOffset JoinedAt, bool IsArchived);

public class GroupService
{
    private readonly AppDbContext _appDbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GroupService> _logger;

    public GroupService(AppDbContext appDbContext, TimeProvider timeProvider, ILogger<GroupService> logger)
    {
        _appDbContext = appDbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Group> CreateAsync(string teacherId, string name, CancellationToken ct = default)
    {
        var group = Group.Create(teacherId, name, _timeProvider.GetUtcNow());
        _appDbContext.Groups.Add(group);
        await _appDbContext.SaveChangesAsync(ct);
        _logger.LogInformation("GroupService: group {GroupId} created by {TeacherId}", group.Id, teacherId);
        return group;
    }

    public async Task<List<Group>> ListAsync(string teacherId, bool includeArchived, CancellationToken ct = default)
    {
        var groups = await _appDbContext.Groups.Where(g => g.TeacherId == teacherId).ToListAsync(ct);
        return groups
            .Where(g => includeArchived || !g.IsArchived)
            .OrderBy(g => g.Name)
            .ToList();
    }

    public Task<Group?> GetOwnedAsync(Guid groupId, string teacherId, CancellationToken ct = default) =>
        _appDbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.TeacherId == teacherId, ct);

    public async Task<Group?> RenameAsync(Guid groupId, string teacherId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));
        var group = await GetOwnedAsync(groupId, teacherId, ct);
        if (group is null) return null;

        group.Name = name.Trim();
        await _appDbContext.SaveChangesAsync(ct);
        return group;
    }

    public async Task<bool> ArchiveAsync(Guid groupId, string teacherId, CancellationToken ct = default)
    {
        var group = await GetOwnedAsync(groupId, teacherId, ct);
        if (group is null) return false;

        group.ArchivedAt ??= _timeProvider.GetUtcNow();
        var now = _timeProvider.GetUtcNow();
        foreach (var invite in await _appDbContext.GroupInvites.Where(i => i.GroupId == groupId && i.RevokedAt == null).ToListAsync(ct))
            invite.RevokedAt = now;

        await _appDbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<GroupInvite>> ListInvitesAsync(Guid groupId, string teacherId, CancellationToken ct = default)
    {
        if (await GetOwnedAsync(groupId, teacherId, ct) is null) return [];
        var invites = await _appDbContext.GroupInvites.Where(i => i.GroupId == groupId).ToListAsync(ct);
        return invites.OrderByDescending(i => i.ExpiresAt).ToList();
    }

    public async Task<GroupInvite?> IssueInviteAsync(Guid groupId, string teacherId, TimeSpan lifetime, int? maxUses, CancellationToken ct = default)
    {
        var group = await GetOwnedAsync(groupId, teacherId, ct);
        if (group is null || group.IsArchived) return null;

        var invite = GroupInvite.Create(groupId, _timeProvider.GetUtcNow(), lifetime, maxUses);
        _appDbContext.GroupInvites.Add(invite);
        await _appDbContext.SaveChangesAsync(ct);
        return invite;
    }

    public async Task<bool> RevokeInviteAsync(Guid groupId, Guid inviteId, string teacherId, CancellationToken ct = default)
    {
        if (await GetOwnedAsync(groupId, teacherId, ct) is null) return false;
        var invite = await _appDbContext.GroupInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.GroupId == groupId, ct);
        if (invite is null) return false;

        invite.RevokedAt ??= _timeProvider.GetUtcNow();
        await _appDbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<GroupInvite?> ReissueInviteAsync(Guid groupId, string teacherId, TimeSpan lifetime, int? maxUses, CancellationToken ct = default)
    {
        var group = await GetOwnedAsync(groupId, teacherId, ct);
        if (group is null || group.IsArchived) return null;

        var now = _timeProvider.GetUtcNow();
        foreach (var old in await _appDbContext.GroupInvites.Where(i => i.GroupId == groupId && i.RevokedAt == null).ToListAsync(ct))
            old.RevokedAt = now;

        var invite = GroupInvite.Create(groupId, now, lifetime, maxUses);
        _appDbContext.GroupInvites.Add(invite);
        await _appDbContext.SaveChangesAsync(ct);
        return invite;
    }

    public async Task<bool> RemoveStudentAsync(Guid groupId, string studentId, string teacherId, CancellationToken ct = default)
    {
        if (await GetOwnedAsync(groupId, teacherId, ct) is null) return false;
        var member = await _appDbContext.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.StudentId == studentId, ct);
        if (member is null) return false;

        _appDbContext.GroupMembers.Remove(member);
        await _appDbContext.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Only students of the teacher's own group and only those with recorded consent. Another teacher's group returns null.</summary>
    public async Task<List<StudentStatus>?> GetStudentsAsync(Guid groupId, string teacherId, CancellationToken ct = default, string? level = null, string? variant = null)
    {
        if (await GetOwnedAsync(groupId, teacherId, ct) is null) return null;

        var members = (await _appDbContext.GroupMembers.Where(m => m.GroupId == groupId && m.ConsentAt != null).ToListAsync(ct))
            .OrderBy(m => m.JoinedAt)
            .ToList();

        var result = new List<StudentStatus>();
        foreach (var member in members)
        {
            if (!Guid.TryParse(member.StudentId, out var userId)) continue;
            var student = await _appDbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (student is null) continue;

            var attempts = await _appDbContext.ExamAttempts.Where(a => a.UserId == member.StudentId).ToListAsync(ct);
            if (level is not null && variant is not null)
            {
                var ids = await _appDbContext.Arkusze.Where(a => a.Level == level && a.Variant == variant).Select(a => a.Id).ToListAsync(ct);
                attempts = attempts.Where(a => ids.Contains(a.ArkuszId)).ToList();
            }

            var patterns = await _appDbContext.ErrorPatterns.Where(p => p.UserId == member.StudentId).ToListAsync(ct);
            var reviewDates = await _appDbContext.ReviewSessions.Where(s => s.UserId == member.StudentId).Select(s => s.Date).ToListAsync(ct);
            result.Add(StudentStatus.Build(student, member, attempts, patterns, reviewDates));
        }

        return result;
    }

    public async Task<InvitePreview> PreviewAsync(string token, CancellationToken ct = default)
    {
        var invite = await _appDbContext.GroupInvites.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null) return new InvitePreview(JoinOutcome.NotFound, null, null, null);

        var group = await _appDbContext.Groups.FirstAsync(g => g.Id == invite.GroupId, ct);
        var teacherName = await TeacherNameAsync(group.TeacherId, ct);
        var state = group.IsArchived ? JoinOutcome.Archived : Map(invite.Evaluate(_timeProvider.GetUtcNow()));
        return new InvitePreview(state, group.Id, group.Name, teacherName);
    }

    public async Task<JoinOutcome> JoinAsync(string token, string studentId, bool consent, CancellationToken ct = default)
    {
        var invite = await _appDbContext.GroupInvites.FirstOrDefaultAsync(i => i.Token == token, ct);
        if (invite is null) return JoinOutcome.NotFound;

        var group = await _appDbContext.Groups.FirstAsync(g => g.Id == invite.GroupId, ct);
        if (group.TeacherId == studentId) return JoinOutcome.OwnGroup;
        if (group.IsArchived) return JoinOutcome.Archived;

        var existing = await _appDbContext.GroupMembers.AnyAsync(m => m.GroupId == group.Id && m.StudentId == studentId, ct);
        if (existing) return JoinOutcome.AlreadyMember;

        var state = invite.Evaluate(_timeProvider.GetUtcNow());
        if (state != InviteState.Ok) return Map(state);
        if (!consent) return JoinOutcome.ConsentRequired;

        // The counter is a conditional UPDATE in the DB, not invite.Uses++: two simultaneous joins on the last place cannot exceed the limit.
        await using var transaction = await _appDbContext.Database.BeginTransactionAsync(ct);
        var claimed = await _appDbContext.GroupInvites
            .Where(i => i.Id == invite.Id && i.RevokedAt == null && (i.MaxUses == null || i.Uses < i.MaxUses))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Uses, i => i.Uses + 1), ct);
        await _appDbContext.Entry(invite).ReloadAsync(ct);
        if (claimed == 0)
        {
            var current = invite.Evaluate(_timeProvider.GetUtcNow());
            return current == InviteState.Ok ? JoinOutcome.Exhausted : Map(current);
        }

        var now = _timeProvider.GetUtcNow();
        _appDbContext.GroupMembers.Add(new GroupMember(group.Id, studentId, now, now));
        await _appDbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _logger.LogInformation("GroupService: student {StudentId} joined group {GroupId}", studentId, group.Id);
        return JoinOutcome.Joined;
    }

    public async Task<bool> LeaveAsync(Guid groupId, string studentId, CancellationToken ct = default)
    {
        var member = await _appDbContext.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.StudentId == studentId, ct);
        if (member is null) return false;

        _appDbContext.GroupMembers.Remove(member);
        await _appDbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<MyGroup>> MyGroupsAsync(string studentId, CancellationToken ct = default)
    {
        var memberships = await _appDbContext.GroupMembers.Where(m => m.StudentId == studentId).ToListAsync(ct);
        var result = new List<MyGroup>();
        foreach (var member in memberships.OrderBy(m => m.JoinedAt))
        {
            var group = await _appDbContext.Groups.FirstAsync(g => g.Id == member.GroupId, ct);
            result.Add(new MyGroup(group.Id, group.Name, await TeacherNameAsync(group.TeacherId, ct), member.JoinedAt, group.IsArchived));
        }

        return result;
    }

    private async Task<string> TeacherNameAsync(string teacherId, CancellationToken ct)
    {
        if (!Guid.TryParse(teacherId, out var id)) return teacherId;
        var teacher = await _appDbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        return teacher?.DisplayName ?? teacherId;
    }

    private static JoinOutcome Map(InviteState state) => state switch
    {
        InviteState.Revoked => JoinOutcome.Revoked,
        InviteState.Expired => JoinOutcome.Expired,
        InviteState.Exhausted => JoinOutcome.Exhausted,
        _ => JoinOutcome.Joined,
    };
}
