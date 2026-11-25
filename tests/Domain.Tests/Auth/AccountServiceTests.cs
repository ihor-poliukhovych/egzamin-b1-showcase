using B1.Domain.Auth;
using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using B1.Domain.Groups;
using B1.Infrastructure.Auth;
using B1.Infrastructure.Groups;
using B1.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace B1.Domain.Tests.Auth;

public class AccountServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _sut = new AccountService(_db, NullLogger<AccountService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private User AddUser(string subject, string email, UserRole role = UserRole.Student)
    {
        var user = User.Create(subject, email, email, role, Now);
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    /// <summary>A full set of user data: an attempt with an answer, an exam session, a pattern with an attempt, a blitz.</summary>
    private void AddFullDataFor(User user)
    {
        var arkusz = Arkusz.Create($"A-{user.Email}", "test", ArkuszSource.Official, []);
        _db.Arkusze.Add(arkusz);
        var session = new ExamSession(Guid.NewGuid(), user.UserKey, arkusz.Id, Now);
        _db.ExamSessions.Add(session);
        var attempt = new ExamAttempt(Guid.NewGuid(), user.UserKey, arkusz.Id, PartKind.Gramatyka, Now, Now.AddMinutes(30), 20m, 30m, []) { SessionId = session.Id };
        attempt.Answers.Add(ExamAnswer.Create(attempt.Id, Guid.NewGuid(), "odpowiedź", Now));
        _db.ExamAttempts.Add(attempt);

        var pattern = new ErrorPattern(user.UserKey, "R1", "zły", "dobry", "reguła", PatternCategory.Gramatyka);
        pattern.Record(AttemptResult.Fail, DateOnly.FromDateTime(Now.UtcDateTime));
        _db.ErrorPatterns.Add(pattern);
        _db.ReviewSessions.Add(new ReviewSession(user.UserKey, DateOnly.FromDateTime(Now.UtcDateTime), [pattern.Id]));
        _db.SaveChanges();
    }

    [Fact]
    public async Task DeleteAsync_WhenUserHasData_RemovesUserAndAllTheirData()
    {
        var anna = AddUser("s1", "anna@example.com");
        AddFullDataFor(anna);

        var deleted = await _sut.DeleteAsync(anna.Id);

        deleted.Should().BeTrue();
        (await _db.Users.AnyAsync(u => u.Id == anna.Id)).Should().BeFalse();
        (await _db.ExamAttempts.CountAsync()).Should().Be(0);
        (await _db.ExamAnswers.CountAsync()).Should().Be(0);
        (await _db.ExamSessions.CountAsync()).Should().Be(0);
        (await _db.ErrorPatterns.CountAsync()).Should().Be(0);
        (await _db.Attempts.CountAsync()).Should().Be(0);
        (await _db.ReviewSessions.CountAsync()).Should().Be(0);
        (await _db.ReviewItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_WhenOtherUserHasData_KeepsTheirData()
    {
        var anna = AddUser("s1", "anna@example.com");
        var piotr = AddUser("s2", "piotr@example.com");
        AddFullDataFor(anna);
        AddFullDataFor(piotr);

        await _sut.DeleteAsync(anna.Id);

        (await _db.Users.AnyAsync(u => u.Id == piotr.Id)).Should().BeTrue();
        (await _db.ExamAttempts.CountAsync(a => a.UserId == piotr.UserKey)).Should().Be(1);
        (await _db.ExamAnswers.CountAsync()).Should().Be(1);
        (await _db.ExamSessions.CountAsync(s => s.UserId == piotr.UserKey)).Should().Be(1);
        (await _db.ErrorPatterns.CountAsync(p => p.UserId == piotr.UserKey)).Should().Be(1);
        (await _db.Attempts.CountAsync()).Should().Be(1);
        (await _db.ReviewItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenStudentInGroup_RemovesMembershipButKeepsGroup()
    {
        var teacher = AddUser("t1", "teacher@example.com", UserRole.Teacher);
        var anna = AddUser("s1", "anna@example.com");
        var groups = new GroupService(_db, TimeProvider.System, NullLogger<GroupService>.Instance);
        var group = await groups.CreateAsync(teacher.UserKey, "B1");
        var invite = (await groups.IssueInviteAsync(group.Id, teacher.UserKey, TimeSpan.FromDays(7), null))!;
        await groups.JoinAsync(invite.Token, anna.UserKey, consent: true);

        await _sut.DeleteAsync(anna.Id);

        (await _db.GroupMembers.CountAsync()).Should().Be(0);
        (await _db.Groups.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenTeacher_RemovesTheirGroupsMembersAndInvites()
    {
        var teacher = AddUser("t1", "teacher@example.com", UserRole.Teacher);
        var anna = AddUser("s1", "anna@example.com");
        var groups = new GroupService(_db, TimeProvider.System, NullLogger<GroupService>.Instance);
        var group = await groups.CreateAsync(teacher.UserKey, "B1");
        var invite = (await groups.IssueInviteAsync(group.Id, teacher.UserKey, TimeSpan.FromDays(7), null))!;
        await groups.JoinAsync(invite.Token, anna.UserKey, consent: true);

        await _sut.DeleteAsync(teacher.Id);

        (await _db.Groups.CountAsync()).Should().Be(0);
        (await _db.GroupMembers.CountAsync()).Should().Be(0);
        (await _db.GroupInvites.CountAsync()).Should().Be(0);
        (await _db.Users.AnyAsync(u => u.Id == anna.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenUserMissing_ReturnsFalse()
    {
        var deleted = await _sut.DeleteAsync(Guid.NewGuid());

        deleted.Should().BeFalse();
    }
}
