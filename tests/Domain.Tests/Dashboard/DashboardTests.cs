using B1.Domain.Auth;
using B1.Domain.Dashboard;
using B1.Domain.Exam;
using B1.Domain.Groups;
using B1.Infrastructure.Dashboard;
using B1.Infrastructure.Groups;
using B1.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace B1.Domain.Tests.Dashboard;

public class StudentReadinessTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly Guid _arkuszId = Guid.NewGuid();

    private ExamAttempt Done(PartKind kind, decimal points, int dayOffset, decimal max = 30m) =>
        new(Guid.NewGuid(), "u1", _arkuszId, kind, T0.AddDays(dayOffset), T0.AddDays(dayOffset).AddMinutes(30), points, max, []);

    [Fact]
    public void Build_WhenNoAttempts_ReturnsFivePartsNotStartedWithoutTrend()
    {
        var actual = StudentReadiness.Build([]);

        actual.Should().HaveCount(5);
        actual.Should().OnlyContain(p => p.Last.Status == PartStatus.NotStarted && p.Direction == TrendDirection.None && p.Trend.Count == 0);
        actual.Single(p => p.Kind == PartKind.Mowienie).Last.MaxPoints.Should().Be(40m);
    }

    [Fact]
    public void Build_WhenSeveralAttempts_UsesLatestAndFlagsBelowHalf()
    {
        var attempts = new[] { Done(PartKind.Gramatyka, 25m, 1), Done(PartKind.Gramatyka, 14m, 3) };

        var actual = StudentReadiness.Build(attempts).Single(p => p.Kind == PartKind.Gramatyka);

        actual.Last.Points.Should().Be(14m);
        actual.Last.IsBelowThreshold.Should().BeTrue();
        actual.Trend.Should().Equal(83.3m, 46.7m);
        actual.Direction.Should().Be(TrendDirection.Down);
    }

    [Fact]
    public void Build_WhenMoreThanFiveAttempts_KeepsLastFiveOldestFirst()
    {
        var attempts = Enumerable.Range(1, 7).Select(i => Done(PartKind.Czytanie, i * 3m, i)).ToList();

        var actual = StudentReadiness.Build(attempts).Single(p => p.Kind == PartKind.Czytanie);

        actual.Trend.Should().Equal(30m, 40m, 50m, 60m, 70m);
        actual.Direction.Should().Be(TrendDirection.Up);
    }

    [Theory]
    [InlineData(new[] { 50.0, 51.5 }, TrendDirection.Flat)]
    [InlineData(new[] { 50.0, 53.0 }, TrendDirection.Up)]
    [InlineData(new[] { 50.0, 47.0 }, TrendDirection.Down)]
    [InlineData(new[] { 50.0 }, TrendDirection.None)]
    public void DirectionOf_WhenTrendGiven_UsesTwoPointTolerance(double[] values, TrendDirection expected)
    {
        var actual = StudentReadiness.DirectionOf(values.Select(v => (decimal)v).ToList());

        actual.Should().Be(expected);
    }

    [Fact]
    public void BestMatrix_WhenRepeatedAttempts_KeepsBestPercentPerArkuszAndPart()
    {
        var codes = new Dictionary<Guid, string> { [_arkuszId] = "2022-03" };
        var attempts = new[] { Done(PartKind.Sluchanie, 15m, 1), Done(PartKind.Sluchanie, 24m, 2), Done(PartKind.Sluchanie, 18m, 3) };

        var actual = StudentReadiness.BestMatrix(attempts, codes);

        actual["2022-03"][PartKind.Sluchanie].Should().Be(80m);
        actual["2022-03"].Should().HaveCount(1);
    }

    [Fact]
    public void SuggestNext_WhenSomeArkuszAlreadyDone_ReturnsNewestUntouchedElseNewest()
    {
        var codes = new Dictionary<Guid, string> { [_arkuszId] = "2022-03" };
        var attempts = new[] { Done(PartKind.Sluchanie, 15m, 1) };

        StudentReadiness.SuggestNext(["2023-04", "2022-03", "2024-06"], attempts, codes).Should().Be("2024-06");
        StudentReadiness.SuggestNext(["2022-03"], attempts, codes).Should().Be("2022-03");
        StudentReadiness.SuggestNext([], attempts, codes).Should().BeNull();
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(30, true)]
    public void IsInactive_WhenDaysSinceLastActivity_FlagsAtSevenDaysOrMore(int days, bool expected)
    {
        var now = T0.AddDays(60);

        StudentReadiness.IsInactive(now.AddDays(-days), now).Should().Be(expected);
    }

    [Fact]
    public void IsInactive_WhenNeverActive_IsInactive()
    {
        StudentReadiness.IsInactive(null, T0).Should().BeTrue();
    }

    [Fact]
    public void GroupSummary_WhenStudentsMixed_CountsBelowHalfPerPartAndListsInactive()
    {
        var now = T0.AddDays(30);
        StudentStatus Student(string name, decimal? gram, DateTimeOffset? active)
        {
            var parts = SessionScorer.AllParts
                .Select(k => k == PartKind.Gramatyka && gram is not null
                    ? new PartResult(k, PartStatus.Submitted, gram, 30m, Guid.NewGuid(), now)
                    : new PartResult(k, PartStatus.NotStarted, null, 30m, null, null))
                .ToList();
            return new StudentStatus(name, name, name + "@x.pl", T0, parts, [], active);
        }

        var students = new[] { Student("Anna", 10m, now.AddDays(-1)), Student("Piotr", 20m, now.AddDays(-9)), Student("Ola", null, null) };

        var actual = GroupSummary.Build(Guid.NewGuid(), "B1", students, now);

        actual.Students.Should().Be(3);
        actual.BelowByPart[PartKind.Gramatyka].Should().Be(1);
        actual.BelowByPart[PartKind.Sluchanie].Should().Be(0);
        actual.InactiveStudents.Should().BeEquivalentTo("Piotr", "Ola");
        actual.PassedByPart[PartKind.Gramatyka].Should().Be(1);
        actual.PassedByPart[PartKind.Sluchanie].Should().Be(0);
        actual.Members.Select(m => m.DisplayName).Should().Equal("Anna", "Ola", "Piotr");
        actual.Members.Single(m => m.DisplayName == "Piotr").Percent[PartKind.Gramatyka].Should().BeApproximately(66.7m, 0.1m);
        actual.Members.Single(m => m.DisplayName == "Ola").Percent[PartKind.Gramatyka].Should().BeNull();
    }
}

public class TeacherAccessTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly DashboardService _sut;
    private readonly GroupService _groups;
    private readonly User _teacher;
    private readonly User _other;
    private readonly User _student;

    public TeacherAccessTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _groups = new GroupService(_db, TimeProvider.System, NullLogger<GroupService>.Instance);
        _sut = new DashboardService(_db, _groups, TimeProvider.System);
        _teacher = Add("t1", "t@x.pl", UserRole.Teacher);
        _other = Add("t2", "o@x.pl", UserRole.Teacher);
        _student = Add("s1", "s@x.pl", UserRole.Student);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private User Add(string subject, string email, UserRole role)
    {
        var user = User.Create(subject, email, subject, role, DateTimeOffset.UtcNow);
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task GetForTeacherAsync_WhenStudentConsentedInOwnGroup_ReturnsDashboard()
    {
        var group = await _groups.CreateAsync(_teacher.UserKey, "B1");
        var invite = (await _groups.IssueInviteAsync(group.Id, _teacher.UserKey, TimeSpan.FromDays(1), null))!;
        await _groups.JoinAsync(invite.Token, _student.UserKey, consent: true);

        var actual = await _sut.GetForTeacherAsync(group.Id, _teacher.UserKey, _student.UserKey);

        actual.Should().NotBeNull();
        actual!.Readiness.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetForTeacherAsync_WhenStudentAlsoInOtherTeachersGroup_HidesOtherGroups()
    {
        var own = await _groups.CreateAsync(_teacher.UserKey, "B1");
        var foreign = await _groups.CreateAsync(_other.UserKey, "Marta C1");
        await _groups.JoinAsync((await _groups.IssueInviteAsync(own.Id, _teacher.UserKey, TimeSpan.FromDays(1), null))!.Token, _student.UserKey, consent: true);
        await _groups.JoinAsync((await _groups.IssueInviteAsync(foreign.Id, _other.UserKey, TimeSpan.FromDays(1), null))!.Token, _student.UserKey, consent: true);

        var actual = await _sut.GetForTeacherAsync(own.Id, _teacher.UserKey, _student.UserKey);

        actual!.Groups.Select(g => g.GroupId).Should().Equal(own.Id);
    }

    [Fact]
    public async Task GetForTeacherAsync_WhenOtherTeacherOrNonMemberOrNoConsent_ReturnsNull()
    {
        var group = await _groups.CreateAsync(_teacher.UserKey, "B1");

        (await _sut.GetForTeacherAsync(group.Id, _teacher.UserKey, _student.UserKey)).Should().BeNull();

        _db.GroupMembers.Add(new GroupMember(group.Id, _student.UserKey, DateTimeOffset.UtcNow, consentAt: null));
        await _db.SaveChangesAsync();

        (await _sut.GetForTeacherAsync(group.Id, _teacher.UserKey, _student.UserKey)).Should().BeNull();
        (await _sut.GetForTeacherAsync(group.Id, _other.UserKey, _student.UserKey)).Should().BeNull();
    }

    [Fact]
    public async Task GetTeacherSummaryAsync_WhenTeacherHasGroups_ReturnsOnlyOwnGroups()
    {
        await _groups.CreateAsync(_teacher.UserKey, "A");
        await _groups.CreateAsync(_other.UserKey, "B");

        var actual = await _sut.GetTeacherSummaryAsync(_teacher.UserKey);

        actual.Select(g => g.Name).Should().Equal("A");
    }
}
