using System.Net;
using System.Net.Http.Json;
using B1.Api.Exam;
using B1.Domain.Exam;
using B1.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Api.Tests;

public class ExamFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ExamFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Accept_WhenAnswerNeedsReview_Returns200_ElseReturns409()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var client = await _factory.LoginAsync("accept@example.com");
        var attempt = await TestData.StartAttemptAsync(client, code, "gramatyka");
        var choose = attempt.Arkusz.Tasks.Single(t => t.Number == 1).Items[0].Id;
        var transform = attempt.Arkusz.Tasks.Single(t => t.Number == 2).Items[0].Id;
        await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/answers", new { answers = new[] { new { itemId = choose, value = "tą" }, new { itemId = transform, value = "Brat otrzymał książkę od Oli" } } });
        (await client.PostAsync($"/exam-attempts/{attempt.Id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var correct = await client.PostAsync($"/exam-attempts/{attempt.Id}/answers/{choose}/accept", null);
        var needsReview = await client.PostAsync($"/exam-attempts/{attempt.Id}/answers/{transform}/accept", null);
        var again = await client.PostAsync($"/exam-attempts/{attempt.Id}/answers/{transform}/accept", null);

        correct.StatusCode.Should().Be(HttpStatusCode.Conflict);
        needsReview.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Submit_WhenSentTwiceConcurrently_SubmitsOnceAndFillsErrorBankOnce()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var client = await _factory.LoginAsync("double@example.com");
        var attempt = await TestData.StartAttemptAsync(client, code, "gramatyka");
        var choose = attempt.Arkusz.Tasks.Single(t => t.Number == 1).Items[0].Id;
        await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/answers", new { answers = new[] { new { itemId = choose, value = "tę" } } });

        var responses = await Task.WhenAll(
            client.PostAsync($"/exam-attempts/{attempt.Id}/submit", null),
            client.PostAsync($"/exam-attempts/{attempt.Id}/submit", null));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        TestData.Code(await TestData.ProblemAsync(conflict)).Should().Be("part-submitted");
        var userId = (await TestData.MeAsync(client)).Id;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ErrorPatterns.CountAsync(p => p.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task SaveAnswers_WhenTooLongOrTooMany_Returns400()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var client = await _factory.LoginAsync("limits@example.com");
        var attempt = await TestData.StartAttemptAsync(client, code, "gramatyka");
        var item = attempt.Arkusz.Tasks[0].Items[0].Id;

        var tooLong = await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/answers", new { answers = new[] { new { itemId = item, value = new string('a', 5001) } } });
        var tooMany = await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/answers", new { answers = Enumerable.Repeat(new { itemId = item, value = "x" }, 3) });

        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        TestData.Code(await TestData.ProblemAsync(tooLong)).Should().Be("validation");
        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Details_WhenAssessmentLargerThan64Kb_Returns400()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var client = await _factory.LoginAsync("assessment@example.com");
        var attempt = await TestData.StartAttemptAsync(client, code, "mowienie");

        var response = await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/details", new { assessment = new { notes = new Dictionary<string, string> { ["1"] = new string('n', 70_000) } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finish_WhenNoText_Returns409NoTextWithoutUsingQuota()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var client = await _factory.LoginAsync("empty@example.com");
        var attempt = await TestData.StartAttemptAsync(client, code, "pisanie");
        await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/choose-set", new { set = 1 });

        var response = await client.PostAsync($"/exam-attempts/{attempt.Id}/finish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        TestData.Code(await TestData.ProblemAsync(response)).Should().Be("review-no-text");
        await using var scope = _factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiReviewUsages.CountAsync(u => u.AttemptId == attempt.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Sessions_WhenPartOutOfOrder_Returns409NextPartBlocked()
    {
        var code = await TestData.SeedArkuszAsync(_factory);
        var client = await _factory.LoginAsync("order@example.com");
        var session = await (await client.PostAsJsonAsync("/exam-sessions", new { arkuszCode = code })).Content.ReadFromJsonAsync<SessionDto>(TestData.Json);

        var response = await client.PostAsJsonAsync($"/exam-sessions/{session!.Id}/parts", new { part = "pisanie" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        TestData.Code(await TestData.ProblemAsync(response)).Should().Be("next-part-blocked");
    }
}

/// <summary>Daily AI limit with Review:DailyLimit = 1. Review:Provider is none, so every review fails, but the reservation stays.</summary>
public class AiQuotaTests
{
    [Fact]
    public async Task Finish_WhenReviewFails_KeepsUsageRow_AndNextFinishHitsDailyLimit()
    {
        await using var factory = new ApiFactory { Settings = { ["Review:DailyLimit"] = "1" } };
        var code = await TestData.SeedArkuszAsync(factory);
        var client = await factory.LoginAsync("quota@example.com");
        var attempt = await TestData.StartAttemptAsync(client, code, "pisanie");
        await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/choose-set", new { set = 1 });
        var item = attempt.Arkusz.Tasks[0].Items[0].Id;
        await client.PutAsJsonAsync($"/exam-attempts/{attempt.Id}/answers", new { answers = new[] { new { itemId = item, value = "Sprzedam rower, prawie nowy." } } });

        var first = await client.PostAsync($"/exam-attempts/{attempt.Id}/finish", null);
        var second = await client.PostAsync($"/exam-attempts/{attempt.Id}/finish", null);

        first.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var failed = await TestData.ProblemAsync(first);
        TestData.Code(failed).Should().Be("review-failed");
        failed.GetProperty("detail").GetString().Should().NotContain("Review:Provider");
        TestData.Code(await TestData.ProblemAsync(second)).Should().Be("review-limit");
        var after = await client.GetFromJsonAsync<ExamAttemptDto>($"/exam-attempts/{attempt.Id}", TestData.Json);
        after!.SubmittedAt.Should().BeNull("a failed review leaves the attempt open");
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiReviewUsages.CountAsync(u => u.Kind == AiUsageKind.Review)).Should().Be(1);
    }
}
