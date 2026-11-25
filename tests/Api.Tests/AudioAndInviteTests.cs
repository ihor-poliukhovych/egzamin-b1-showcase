using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using B1.Api.Endpoints;
using AwesomeAssertions;

namespace B1.Api.Tests;

public class AudioAndInviteTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AudioAndInviteTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Audio_WhenRangeRequested_Returns206WithRequestedBytes()
    {
        var client = await _factory.LoginAsync("listener@example.com");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/audio/{ApiFactory.AudioKey}");
        request.Headers.Range = new RangeHeaderValue(100, 199);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange!.ToString().Should().Be("bytes 100-199/1000");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(_factory.AudioBytes[100..200]);
    }

    [Theory]
    [InlineData("..%2Fsecret")]
    [InlineData("..%5Csecret")]
    [InlineData("%2E%2E%2Fsecret")]
    [InlineData("C:%5CWindows%5Cwin")]
    [InlineData("test-key.mp3")]
    public async Task Audio_WhenKeyTriesToLeaveRoot_Returns404(string key)
    {
        var client = await _factory.LoginAsync("listener@example.com");

        var response = await client.GetAsync($"/audio/{key}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(100000000, null)]
    [InlineData(14, 0)]
    [InlineData(14, -3)]
    public async Task IssueInvite_WhenDaysOrMaxUsesOutOfRange_Returns400(int days, int? maxUses)
    {
        var client = await _factory.LoginAsync(ApiFactory.TeacherEmail);
        var group = await (await client.PostAsJsonAsync("/groups", new { name = "B1" })).Content.ReadFromJsonAsync<GroupDto>();

        var issue = await client.PostAsJsonAsync($"/groups/{group!.Id}/invites", new { days, maxUses });
        var reissue = await client.PostAsJsonAsync($"/groups/{group.Id}/invites/reissue", new { days, maxUses });

        issue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reissue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Groups_WhenStudentRequestsGroupStudents_Returns403()
    {
        var owner = await _factory.LoginAsync(ApiFactory.TeacherEmail);
        var group = await (await owner.PostAsJsonAsync("/groups", new { name = "B1" })).Content.ReadFromJsonAsync<GroupDto>();
        var stranger = await _factory.LoginAsync("student2@example.com");

        var students = await stranger.GetAsync($"/groups/{group!.Id}/students");

        students.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
