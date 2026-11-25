using B1.Domain.Auth;
using B1.Infrastructure.Auth;
using B1.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B1.Domain.Tests.Auth;

public class UserProvisionerTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    public UserProvisionerTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private UserProvisioner CreateSut(AuthSettings? settings = null) =>
        new(_db, settings ?? new AuthSettings(), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<UserProvisioner>.Instance);

    [Fact]
    public async Task FindOrCreateAsync_WhenFirstLogin_CreatesStudentWithProfile()
    {
        var sut = CreateSut();

        var actual = await sut.FindOrCreateAsync("google-1", "anna@example.com", "Anna");

        actual.Role.Should().Be(UserRole.Student);
        actual.Email.Should().Be("anna@example.com");
        actual.DisplayName.Should().Be("Anna");
        (await _db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FindOrCreateAsync_WhenFirstLogin_RoleIsNotChosenYet()
    {
        var sut = CreateSut();

        var actual = await sut.FindOrCreateAsync("google-1", "anna@example.com", "Anna");

        actual.RoleChosen.Should().BeFalse();
    }

    [Fact]
    public async Task FindOrCreateAsync_WhenEmailIsConfiguredTeacher_RoleIsChosenAutomatically()
    {
        var sut = CreateSut(new AuthSettings { TeacherEmails = ["teacher@example.com"] });

        var actual = await sut.FindOrCreateAsync("google-2", "teacher@example.com", "Teacher");

        actual.Role.Should().Be(UserRole.Teacher);
        actual.RoleChosen.Should().BeTrue();
    }

    [Fact]
    public void ChooseRole_WhenNotChosenYet_SetsRoleAndTime()
    {
        var now = new DateTimeOffset(2026, 10, 19, 18, 0, 0, TimeSpan.Zero);
        var user = User.Create("google-3", "olga@example.com", "Olga", UserRole.Student, now);

        var actual = user.ChooseRole(UserRole.Teacher, now);

        actual.Should().BeTrue();
        user.Role.Should().Be(UserRole.Teacher);
        user.RoleChosenAt.Should().Be(now);
    }

    [Fact]
    public void ChooseRole_WhenAlreadyChosen_KeepsFirstChoice()
    {
        var now = new DateTimeOffset(2026, 10, 19, 18, 0, 0, TimeSpan.Zero);
        var user = User.Create("google-4", "piotr@example.com", "Piotr", UserRole.Student, now);
        user.ChooseRole(UserRole.Teacher, now);

        var actual = user.ChooseRole(UserRole.Student, now.AddMinutes(5));

        actual.Should().BeFalse();
        user.Role.Should().Be(UserRole.Teacher);
        user.RoleChosenAt.Should().Be(now);
    }

    [Fact]
    public void ChangeRole_WhenAlreadyChosen_SwitchesRoleAndKeepsFirstChoiceTime()
    {
        var now = new DateTimeOffset(2026, 10, 19, 18, 0, 0, TimeSpan.Zero);
        var user = User.Create("google-5", "anna@example.com", "Anna", UserRole.Student, now);
        user.ChooseRole(UserRole.Teacher, now);

        user.ChangeRole(UserRole.Student, now.AddDays(1));

        user.Role.Should().Be(UserRole.Student);
        user.RoleChosenAt.Should().Be(now);
    }

    [Fact]
    public async Task FindOrCreateAsync_WhenSameSubjectLogsInAgain_ReturnsSameUserWithoutDuplicate()
    {
        var sut = CreateSut();
        var first = await sut.FindOrCreateAsync("google-1", "anna@example.com", "Anna");

        var second = await sut.FindOrCreateAsync("google-1", "anna.new@example.com", "Anna K.");

        second.Id.Should().Be(first.Id);
        second.Email.Should().Be("anna.new@example.com");
        (await _db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FindOrCreateAsync_WhenConfiguredTeacherSwitchedToStudent_KeepsStudentOnNextLogin()
    {
        var sut = CreateSut(new AuthSettings { TeacherEmails = ["teacher@example.com"] });
        var user = await sut.FindOrCreateAsync("google-7", "teacher@example.com", "Teacher");
        user.ChangeRole(UserRole.Student, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync();

        var actual = await sut.FindOrCreateAsync("google-7", "teacher@example.com", "Teacher");

        actual.Role.Should().Be(UserRole.Student);
    }

    [Fact]
    public async Task FindOrCreateAsync_WhenEmailInTeacherList_AssignsTeacherIgnoringCase()
    {
        var sut = CreateSut(new AuthSettings { TeacherEmails = ["Damian@Example.com"] });

        var actual = await sut.FindOrCreateAsync("google-2", "damian@example.com", "Damian");

        actual.Role.Should().Be(UserRole.Teacher);
    }

    [Fact]
    public async Task FindOrCreateAsync_WhenExistingStudentLaterAddedToTeacherList_PromotesToTeacher()
    {
        await CreateSut().FindOrCreateAsync("google-3", "damian@example.com", "Damian");

        var actual = await CreateSut(new AuthSettings { TeacherEmails = ["damian@example.com"] }).FindOrCreateAsync("google-3", "damian@example.com", "Damian");

        actual.Role.Should().Be(UserRole.Teacher);
    }

    [Theory]
    [InlineData("a@x.pl", new[] { "a@x.pl" }, UserRole.Teacher)]
    [InlineData("A@X.PL", new[] { " a@x.pl " }, UserRole.Teacher)]
    [InlineData("b@x.pl", new[] { "a@x.pl" }, UserRole.Student)]
    [InlineData("b@x.pl", new string[0], UserRole.Student)]
    public void RoleFor_WhenEmailChecked_MatchesTeacherListCaseInsensitively(string email, string[] teachers, UserRole expected)
    {
        var actual = User.RoleFor(email, teachers);

        actual.Should().Be(expected);
    }
}
