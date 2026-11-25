using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using B1.Api.Endpoints;
using B1.Api.Exam;
using AwesomeAssertions;

namespace B1.Api.Tests;

/// <summary>A user never sees another user's data: every lookup by id is scoped to the owner and answers 404.</summary>
public class CrossUserTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CrossUserTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Attempt_OfAnotherUser_Returns404()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var owner = await _factory.LoginAsync("owner-a@example.com");
        var attempt = await TestData.StartAttemptAsync(owner, code, "gramatyka");
        var other = await _factory.LoginAsync("other-b@example.com");

        (await other.GetAsync($"/exam-attempts/{attempt.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"/exam-attempts/{attempt.Id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/exam-attempts/{attempt.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Session_OfAnotherUser_Returns404()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var owner = await _factory.LoginAsync("owner-a@example.com");
        var created = await owner.PostAsJsonAsync("/exam-sessions", new { arkuszCode = code });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await created.Content.ReadFromJsonAsync<SessionDto>(TestData.Json);
        var other = await _factory.LoginAsync("other-b@example.com");

        (await other.GetAsync($"/exam-sessions/{session!.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pattern_OfAnotherUser_Returns404()
    {
        var owner = await _factory.LoginAsync("owner-a@example.com");
        var created = await owner.PostAsJsonAsync("/patterns", new { title = "z + narzędnik", wrongExample = "z moja", correctForm = "z moją", rule = "", category = "gramatyka" });
        var pattern = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = pattern.GetProperty("id").GetGuid();
        var other = await _factory.LoginAsync("other-b@example.com");

        (await other.GetAsync($"/patterns/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/patterns/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GroupAndStudentCard_OfAnotherTeacher_Return404()
    {
        var teacherA = await _factory.LoginAsync(ApiFactory.TeacherEmail);
        var group = await (await teacherA.PostAsJsonAsync("/groups", new { name = "B1 wieczorem" })).Content.ReadFromJsonAsync<GroupDto>();
        var invite = await (await teacherA.PostAsJsonAsync($"/groups/{group!.Id}/invites", new { days = 7 })).Content.ReadFromJsonAsync<InviteDto>(TestData.Json);
        var student = await _factory.LoginAsync("member@example.com");
        (await student.PostAsJsonAsync($"/join/{invite!.Token}", new { consent = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var studentId = (await TestData.MeAsync(student)).Id;

        var teacherB = await _factory.LoginAsync("teacher-b@example.com");
        (await teacherB.PostAsJsonAsync("/auth/role", new { role = "Teacher" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await teacherA.GetAsync($"/teacher/groups/{group.Id}/students/{studentId}/dashboard")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await teacherB.GetAsync($"/groups/{group.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await teacherB.GetAsync($"/groups/{group.Id}/students")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await teacherB.GetAsync($"/teacher/groups/{group.Id}/students/{studentId}/dashboard")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
