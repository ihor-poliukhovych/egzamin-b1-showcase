using B1.Domain.Auth;
using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using B1.Domain.Groups;
using B1.Infrastructure.Groups;
using B1.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace B1.Domain.Tests.Groups;

public class GroupServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly TestTime _time = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly GroupService _sut;
    private readonly User _teacher;
    private readonly User _otherTeacher;
    private readonly User _student;
    private readonly User _student2;

    public GroupServiceTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _sut = new GroupService(_db, _time, NullLogger<GroupService>.Instance);

        _teacher = AddUser("t1", "teacher@example.com", "Damian", UserRole.Teacher);
        _otherTeacher = AddUser("t2", "teacher2@example.com", "Marta", UserRole.Teacher);
        _student = AddUser("s1", "anna@example.com", "Anna", UserRole.Student);
        _student2 = AddUser("s2", "piotr@example.com", "Piotr", UserRole.Student);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private User AddUser(string subject, string email, string name, UserRole role)
    {
        var user = User.Create(subject, email, name, role, _time.GetUtcNow());
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private async Task<(Group Group, GroupInvite Invite)> GroupWithInviteAsync(int? maxUses = null, int days = 14)
    {
        var group = await _sut.CreateAsync(_teacher.UserKey, "B1 wieczorna");
        var invite = (await _sut.IssueInviteAsync(group.Id, _teacher.UserKey, TimeSpan.FromDays(days), maxUses))!;
        return (group, invite);
    }

    [Fact]
    public async Task JoinAsync_WhenValidInviteAndConsent_CreatesMemberWithConsentAndCountsUse()
    {
        var (group, invite) = await GroupWithInviteAsync();

        var outcome = await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.Joined);
        var member = await _db.GroupMembers.SingleAsync();
        member.GroupId.Should().Be(group.Id);
        member.ConsentAt.Should().Be(_time.GetUtcNow());
        (await _db.GroupInvites.SingleAsync()).Uses.Should().Be(1);
    }

    [Fact]
    public async Task JoinAsync_WhenNoConsent_RefusesAndDoesNotCreateMember()
    {
        var (_, invite) = await GroupWithInviteAsync();

        var outcome = await _sut.JoinAsync(invite.Token, _student.UserKey, consent: false);

        outcome.Should().Be(JoinOutcome.ConsentRequired);
        (await _db.GroupMembers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task JoinAsync_WhenInviteExpired_ReturnsExpired()
    {
        var (_, invite) = await GroupWithInviteAsync(days: 1);
        _time.Advance(TimeSpan.FromDays(2));

        var outcome = await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.Expired);
        (await _db.GroupMembers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task JoinAsync_WhenInviteRevoked_ReturnsRevoked()
    {
        var (group, invite) = await GroupWithInviteAsync();
        await _sut.RevokeInviteAsync(group.Id, invite.Id, _teacher.UserKey);

        var outcome = await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.Revoked);
    }

    [Fact]
    public async Task JoinAsync_WhenUseLimitReached_ReturnsExhaustedForNextStudent()
    {
        var (_, invite) = await GroupWithInviteAsync(maxUses: 1);
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        var outcome = await _sut.JoinAsync(invite.Token, _student2.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.Exhausted);
        (await _db.GroupMembers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task JoinAsync_WhenStudentJoinsTwice_DoesNotDuplicateMemberOrConsumeSecondUse()
    {
        var (_, invite) = await GroupWithInviteAsync(maxUses: 5);
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        var second = await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        second.Should().Be(JoinOutcome.AlreadyMember);
        (await _db.GroupMembers.CountAsync()).Should().Be(1);
        (await _db.GroupInvites.SingleAsync()).Uses.Should().Be(1);
    }

    [Fact]
    public async Task JoinAsync_WhenLastUseTakenConcurrently_ReturnsExhaustedAndDoesNotAddMember()
    {
        var (group, invite) = await GroupWithInviteAsync(maxUses: 1);
        // Another request has already taken the last place; in this context the invite is tracked with the stale Uses = 0.
        await _db.GroupInvites.Where(i => i.Id == invite.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Uses, 1));

        var outcome = await _sut.JoinAsync(invite.Token, _student2.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.Exhausted);
        (await _db.GroupMembers.AnyAsync(m => m.GroupId == group.Id)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, null, "days")]
    [InlineData(-5, null, "days")]
    [InlineData(366, null, "days")]
    [InlineData(int.MaxValue, null, "days")]
    [InlineData(14, 0, "maxUses")]
    [InlineData(14, 1001, "maxUses")]
    public void Validate_WhenOutOfRange_ReturnsError(int days, int? maxUses, string field)
    {
        var actual = GroupInvite.Validate(days, maxUses);

        actual.Keys.Should().Equal(field);
    }

    [Fact]
    public void Validate_WhenInRange_ReturnsNoErrors()
    {
        GroupInvite.Validate(14, null).Should().BeEmpty();
        GroupInvite.Validate(365, 1000).Should().BeEmpty();
    }

    [Fact]
    public async Task JoinAsync_WhenAlreadyMemberAndInviteExhausted_StillReportsAlreadyMember()
    {
        var (_, invite) = await GroupWithInviteAsync(maxUses: 1);
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        var again = await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        again.Should().Be(JoinOutcome.AlreadyMember);
    }

    [Fact]
    public async Task JoinAsync_WhenTokenUnknown_ReturnsNotFound()
    {
        var outcome = await _sut.JoinAsync("no-such-token", _student.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.NotFound);
    }

    [Fact]
    public async Task JoinAsync_WhenTeacherJoinsOwnGroup_ReturnsOwnGroup()
    {
        var (_, invite) = await GroupWithInviteAsync();

        var outcome = await _sut.JoinAsync(invite.Token, _teacher.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.OwnGroup);
    }

    [Fact]
    public async Task JoinAsync_WhenGroupArchived_ReturnsArchived()
    {
        var (group, invite) = await GroupWithInviteAsync();
        await _sut.ArchiveAsync(group.Id, _teacher.UserKey);

        var outcome = await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        outcome.Should().Be(JoinOutcome.Archived);
    }

    [Fact]
    public async Task ReissueInviteAsync_WhenCalled_RevokesOldTokenAndIssuesNewOne()
    {
        var (group, old) = await GroupWithInviteAsync();

        var fresh = await _sut.ReissueInviteAsync(group.Id, _teacher.UserKey, TimeSpan.FromDays(7), null);

        fresh!.Token.Should().NotBe(old.Token);
        (await _sut.JoinAsync(old.Token, _student.UserKey, consent: true)).Should().Be(JoinOutcome.Revoked);
        (await _sut.JoinAsync(fresh.Token, _student.UserKey, consent: true)).Should().Be(JoinOutcome.Joined);
    }

    [Fact]
    public async Task PreviewAsync_WhenValidToken_ShowsGroupAndTeacherName()
    {
        var (group, invite) = await GroupWithInviteAsync();

        var actual = await _sut.PreviewAsync(invite.Token);

        actual.Should().BeEquivalentTo(new InvitePreview(JoinOutcome.Joined, group.Id, "B1 wieczorna", "Damian"));
    }

    [Fact]
    public async Task GetStudentsAsync_WhenOwnTeacher_ReturnsStudentWithStatus()
    {
        // Arrange
        var (group, invite) = await GroupWithInviteAsync();
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);
        var arkusz = Arkusz.Create("2022-03", "marzec 2022", ArkuszSource.Official, []);
        _db.Arkusze.Add(arkusz);
        var attempt = new ExamAttempt(Guid.NewGuid(), _student.UserKey, arkusz.Id, PartKind.Gramatyka, _time.GetUtcNow(), _time.GetUtcNow(), 12m, 30m, []);
        _db.ExamAttempts.Add(attempt);
        var pattern = new ErrorPattern(_student.UserKey, "dbać o + biernik", "x", "y", "r", PatternCategory.Rekcja, null, "R1");
        pattern.Record(AttemptResult.Fail, new DateOnly(2026, 10, 18));
        _db.ErrorPatterns.Add(pattern);
        await _db.SaveChangesAsync();

        // Act
        var actual = await _sut.GetStudentsAsync(group.Id, _teacher.UserKey);

        // Assert
        var status = actual!.Should().ContainSingle().Subject;
        status.DisplayName.Should().Be("Anna");
        status.Parts.Should().HaveCount(5);
        var grammar = status.Parts.Single(p => p.Kind == PartKind.Gramatyka);
        grammar.Points.Should().Be(12m);
        grammar.Percent.Should().Be(40m);
        grammar.IsBelowThreshold.Should().BeTrue();
        status.Parts.Single(p => p.Kind == PartKind.Sluchanie).Status.Should().Be(PartStatus.NotStarted);
        status.TopPatterns.Should().Equal(new PatternBrief("dbać o + biernik", 1));
        status.LastActivityAt.Should().Be(_time.GetUtcNow());
    }

    [Fact]
    public async Task GetStudentsAsync_WhenAnotherTeacherAsks_ReturnsNullAndLeaksNothing()
    {
        var (group, invite) = await GroupWithInviteAsync();
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        var actual = await _sut.GetStudentsAsync(group.Id, _otherTeacher.UserKey);

        actual.Should().BeNull();
    }

    [Fact]
    public async Task GetStudentsAsync_WhenMemberHasNoConsent_DoesNotListThem()
    {
        var (group, _) = await GroupWithInviteAsync();
        _db.GroupMembers.Add(new GroupMember(group.Id, _student.UserKey, _time.GetUtcNow(), consentAt: null));
        await _db.SaveChangesAsync();

        var actual = await _sut.GetStudentsAsync(group.Id, _teacher.UserKey);

        actual.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStudentsAsync_WhenStudentsInOtherGroup_ListsOnlyThisGroupsStudents()
    {
        var (group, invite) = await GroupWithInviteAsync();
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);
        var other = await _sut.CreateAsync(_teacher.UserKey, "Druga grupa");
        var otherInvite = (await _sut.IssueInviteAsync(other.Id, _teacher.UserKey, TimeSpan.FromDays(1), null))!;
        await _sut.JoinAsync(otherInvite.Token, _student2.UserKey, consent: true);

        var actual = await _sut.GetStudentsAsync(group.Id, _teacher.UserKey);

        actual!.Select(s => s.DisplayName).Should().Equal("Anna");
    }

    [Fact]
    public async Task RemoveStudentAsync_WhenOwnTeacher_RemovesMemberButOtherTeacherCannot()
    {
        var (group, invite) = await GroupWithInviteAsync();
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        var byOther = await _sut.RemoveStudentAsync(group.Id, _student.UserKey, _otherTeacher.UserKey);
        var byOwner = await _sut.RemoveStudentAsync(group.Id, _student.UserKey, _teacher.UserKey);

        byOther.Should().BeFalse();
        byOwner.Should().BeTrue();
        (await _db.GroupMembers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ManagementMethods_WhenGroupBelongsToAnotherTeacher_AreRefused()
    {
        var (group, invite) = await GroupWithInviteAsync();

        (await _sut.RenameAsync(group.Id, _otherTeacher.UserKey, "Hacked")).Should().BeNull();
        (await _sut.ArchiveAsync(group.Id, _otherTeacher.UserKey)).Should().BeFalse();
        (await _sut.IssueInviteAsync(group.Id, _otherTeacher.UserKey, TimeSpan.FromDays(1), null)).Should().BeNull();
        (await _sut.RevokeInviteAsync(group.Id, invite.Id, _otherTeacher.UserKey)).Should().BeFalse();
        (await _sut.ListInvitesAsync(group.Id, _otherTeacher.UserKey)).Should().BeEmpty();
    }

    [Fact]
    public async Task MyGroupsAsync_WhenStudentInOneGroup_ShowsOnlyOwnMembershipAndLeaveRemovesIt()
    {
        var (group, invite) = await GroupWithInviteAsync();
        await _sut.JoinAsync(invite.Token, _student.UserKey, consent: true);

        var mine = await _sut.MyGroupsAsync(_student.UserKey);
        var others = await _sut.MyGroupsAsync(_student2.UserKey);
        var left = await _sut.LeaveAsync(group.Id, _student.UserKey);

        mine.Should().ContainSingle().Which.TeacherName.Should().Be("Damian");
        others.Should().BeEmpty();
        left.Should().BeTrue();
        (await _sut.MyGroupsAsync(_student.UserKey)).Should().BeEmpty();
        (await _sut.LeaveAsync(group.Id, _student.UserKey)).Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveAsync_WhenCalled_RevokesActiveInvites()
    {
        var (group, invite) = await GroupWithInviteAsync();

        await _sut.ArchiveAsync(group.Id, _teacher.UserKey);

        (await _db.GroupInvites.SingleAsync(i => i.Id == invite.Id)).RevokedAt.Should().NotBeNull();
        (await _sut.IssueInviteAsync(group.Id, _teacher.UserKey, TimeSpan.FromDays(1), null)).Should().BeNull();
    }

    private sealed class TestTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}

public class GroupInviteTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_WhenFresh_IsOk()
    {
        var invite = GroupInvite.Create(Guid.NewGuid(), Now, TimeSpan.FromDays(1), 3);

        invite.Evaluate(Now).Should().Be(InviteState.Ok);
    }

    [Theory]
    [InlineData(86399, InviteState.Ok)]
    [InlineData(86400, InviteState.Expired)]
    public void Evaluate_WhenAtExpiryBoundary_ExpiresAtExactDeadline(int seconds, InviteState expected)
    {
        var invite = GroupInvite.Create(Guid.NewGuid(), Now, TimeSpan.FromDays(1), null);

        invite.Evaluate(Now.AddSeconds(seconds)).Should().Be(expected);
    }

    [Fact]
    public void Evaluate_WhenRevokedAndExpired_ReportsRevokedFirst()
    {
        var invite = GroupInvite.Create(Guid.NewGuid(), Now, TimeSpan.FromDays(1), null);
        invite.RevokedAt = Now;

        invite.Evaluate(Now.AddDays(5)).Should().Be(InviteState.Revoked);
    }

    [Fact]
    public void Evaluate_WhenUsesReachMax_IsExhausted()
    {
        var invite = GroupInvite.Create(Guid.NewGuid(), Now, TimeSpan.FromDays(1), 2);
        invite.Uses = 2;

        invite.Evaluate(Now).Should().Be(InviteState.Exhausted);
    }

    [Fact]
    public void NewToken_WhenGenerated_Has128BitsAndIsUrlSafeAndUnique()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => GroupInvite.NewToken()).ToList();

        tokens.Should().OnlyHaveUniqueItems();
        tokens.Should().OnlyContain(t => t.Length == 22 && !t.Contains('+') && !t.Contains('/') && !t.Contains('='));
    }
}
