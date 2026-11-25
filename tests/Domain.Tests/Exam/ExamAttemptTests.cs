using B1.Domain.Exam;
using AwesomeAssertions;
using static B1.Domain.Tests.Exam.ArkuszFactory;

namespace B1.Domain.Tests.Exam;

public class ExamAttemptTests
{
    private readonly Arkusz _arkusz;
    private readonly ExamAttempt _attempt;

    public ExamAttemptTests()
    {
        _arkusz = TwoTaskArkusz();
        _attempt = AttemptFor(_arkusz);
    }

    [Fact]
    public void Create_WhenArkuszGiven_CopiesMaxPointsAndStartsUnsubmitted()
    {
        _attempt.MaxPoints.Should().Be(3m);
        _attempt.IsSubmitted.Should().BeFalse();
        _attempt.Answers.Should().BeEmpty();
    }

    [Fact]
    public void SaveAnswer_WhenItemAnsweredTwice_OverwritesValueWithoutDuplicate()
    {
        var itemId = _arkusz.Parts[0].Tasks[0].Items[0].Id;

        _attempt.SaveAnswer(itemId, "tę", StartedAt);
        _attempt.SaveAnswer(itemId, "tą", StartedAt.AddMinutes(1));

        _attempt.Answers.Should().HaveCount(1);
        _attempt.FindAnswer(itemId)!.Value.Should().Be("tą");
        _attempt.FindAnswer(itemId)!.UpdatedAt.Should().Be(StartedAt.AddMinutes(1));
    }

    [Fact]
    public void SaveAnswer_WhenAttemptSubmitted_Throws()
    {
        ExamGrader.Submit(_attempt, _arkusz.Parts[0], SubmittedAt);

        var act = () => _attempt.SaveAnswer(_arkusz.Parts[0].Tasks[0].Items[0].Id, "tą", SubmittedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RomanNumber_WhenTaskNumberWithinExam_ReturnsRomanNumeral()
    {
        var task = ChooseTask(_arkusz.Parts[0].Id, number: 8);

        task.RomanNumber.Should().Be("VIII");
    }
}

public class ExamAttemptAudioPositionTests
{
    [Fact]
    public void SaveAudioPosition_WhenAttemptOpen_StoresPositionAndClampsNegativeToZero()
    {
        var attempt = ArkuszFactory.AttemptFor(ArkuszFactory.TwoTaskArkusz());

        attempt.SaveAudioPosition(125.5);
        attempt.AudioPositionSec.Should().Be(125.5);

        attempt.SaveAudioPosition(-3);
        attempt.AudioPositionSec.Should().Be(0);
    }

    [Fact]
    public void SaveAudioPosition_WhenAttemptSubmitted_Throws()
    {
        var arkusz = ArkuszFactory.TwoTaskArkusz();
        var attempt = ArkuszFactory.AttemptFor(arkusz);
        ExamGrader.Submit(attempt, arkusz.Parts[0], ArkuszFactory.SubmittedAt);

        var act = () => attempt.SaveAudioPosition(10);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SaveAudioPosition_WhenNotFinite_Throws()
    {
        var attempt = ArkuszFactory.AttemptFor(ArkuszFactory.TwoTaskArkusz());

        var act = () => attempt.SaveAudioPosition(double.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
