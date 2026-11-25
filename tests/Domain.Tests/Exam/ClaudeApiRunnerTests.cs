using B1.Domain.Exam;
using B1.Infrastructure.Review;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace B1.Domain.Tests.Exam;

public class ClaudeApiRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenApiKeyMissing_ThrowsReviewExceptionWithoutCallingApi()
    {
        var sut = new ClaudeApiRunner(Options.Create(new ReviewOptions { Provider = "api", ApiKey = "" }), NullLogger<ClaudeApiRunner>.Instance);

        var act = () => sut.RunAsync("prompt", CancellationToken.None);

        await act.Should().ThrowAsync<ReviewException>().WithMessage("*Review:ApiKey*");
    }

    [Fact]
    public void ApiModel_WhenNotConfigured_DefaultsToCurrentOpus()
    {
        new ReviewOptions().ApiModel.Should().Be("claude-opus-5-5");
    }
}
