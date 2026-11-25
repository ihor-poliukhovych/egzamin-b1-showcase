using B1.Domain.Exam;
using AwesomeAssertions;
using static B1.Domain.Tests.Exam.ArkuszFactory;

namespace B1.Domain.Tests.Exam;

public class SessionScorerTests
{
    private readonly Arkusz _arkusz = Arkusz.Create("2022-03", "marzec 2022", ArkuszSource.Official, []);

    private ExamAttempt Submitted(PartKind kind, decimal points) =>
        new(Guid.NewGuid(), "student-1", _arkusz.Id, kind, StartedAt, SubmittedAt, points, 30m, []);

    private ExamAttempt Open(PartKind kind) =>
        new(Guid.NewGuid(), "student-1", _arkusz.Id, kind, StartedAt, null, 0m, 30m, []);

    private static PartResult Scored(PartKind kind, decimal points, decimal max = 30m) =>
        new(kind, PartStatus.Submitted, points, max, Guid.NewGuid(), SubmittedAt);

    [Fact]
    public void Build_WhenNoAttempts_NextPartIsSluchanieAndVerdictInProgress()
    {
        var actual = SessionScorer.Build(_arkusz, []);

        actual.NextPart.Should().Be(PartKind.Sluchanie);
        actual.Verdict.Should().Be(SessionVerdict.InProgress);
        actual.Parts.Select(p => p.Kind).Should().Equal(SessionScorer.AllParts);
    }

    [Fact]
    public void Build_WhenSluchanieSubmitted_NextPartIsCzytanie()
    {
        var actual = SessionScorer.Build(_arkusz, [Submitted(PartKind.Sluchanie, 20m)]);

        actual.NextPart.Should().Be(PartKind.Czytanie);
        actual.Parts[0].Points.Should().Be(20m);
        actual.Parts[0].Status.Should().Be(PartStatus.Submitted);
    }

    [Fact]
    public void Build_WhenPartInProgress_KeepsItAsNextPartAndHasNoPoints()
    {
        var actual = SessionScorer.Build(_arkusz, [Submitted(PartKind.Sluchanie, 20m), Open(PartKind.Czytanie)]);

        actual.NextPart.Should().Be(PartKind.Czytanie);
        actual.Parts[1].Status.Should().Be(PartStatus.InProgress);
        actual.Parts[1].Points.Should().BeNull();
    }

    [Fact]
    public void Build_WhenFirstFourPartsSubmitted_NextPartIsMowienieDayTwo()
    {
        var attempts = new[] { Submitted(PartKind.Sluchanie, 20m), Submitted(PartKind.Czytanie, 25m), Submitted(PartKind.Gramatyka, 15m), Submitted(PartKind.Pisanie, 18m) };

        var actual = SessionScorer.Build(_arkusz, attempts);

        actual.NextPart.Should().Be(PartKind.Mowienie);
        actual.Verdict.Should().Be(SessionVerdict.InProgress);
        actual.Parts[4].MaxPoints.Should().Be(40m);
    }

    [Fact]
    public void Build_WhenAllFivePartsSubmittedAtOrAboveHalf_VerdictPassedAndNoNextPart()
    {
        var speaking = new ExamAttempt(Guid.NewGuid(), "student-1", _arkusz.Id, PartKind.Mowienie, StartedAt, SubmittedAt, 20m, 40m, []);
        var attempts = new[] { Submitted(PartKind.Sluchanie, 20m), Submitted(PartKind.Czytanie, 25m), Submitted(PartKind.Gramatyka, 15m), Submitted(PartKind.Pisanie, 18m), speaking };

        var actual = SessionScorer.Build(_arkusz, attempts);

        actual.NextPart.Should().BeNull();
        actual.Verdict.Should().Be(SessionVerdict.Passed);
    }

    [Fact]
    public void Build_WhenOnePartBelowHalfEvenIfOthersPending_VerdictFailed()
    {
        var actual = SessionScorer.Build(_arkusz, [Submitted(PartKind.Sluchanie, 14m)]);

        actual.Parts[0].Percent.Should().Be(46.7m);
        actual.Parts[0].IsBelowThreshold.Should().BeTrue();
        actual.Verdict.Should().Be(SessionVerdict.Failed);
    }

    [Theory]
    [InlineData(15, false)]
    [InlineData(14.5, true)]
    [InlineData(30, false)]
    [InlineData(0, true)]
    public void IsBelowThreshold_WhenPercentAroundFifty_IsStrictlyBelowOnly(double points, bool expected)
    {
        var actual = Scored(PartKind.Gramatyka, (decimal)points).IsBelowThreshold;

        actual.Should().Be(expected);
    }

    [Fact]
    public void DecideVerdict_WhenAllFivePartsScoredAtOrAboveHalf_Passed()
    {
        var parts = new List<PartResult>
        {
            Scored(PartKind.Sluchanie, 15m), Scored(PartKind.Czytanie, 30m), Scored(PartKind.Gramatyka, 20m),
            Scored(PartKind.Pisanie, 15m), Scored(PartKind.Mowienie, 20m, 40m),
        };

        var actual = SessionScorer.DecideVerdict(parts);

        actual.Should().Be(SessionVerdict.Passed);
    }

    [Fact]
    public void DecideVerdict_WhenSpeakingBelowHalfOfForty_Failed()
    {
        var parts = new List<PartResult>
        {
            Scored(PartKind.Sluchanie, 30m), Scored(PartKind.Czytanie, 30m), Scored(PartKind.Gramatyka, 30m),
            Scored(PartKind.Pisanie, 30m), Scored(PartKind.Mowienie, 19m, 40m),
        };

        var actual = SessionScorer.DecideVerdict(parts);

        actual.Should().Be(SessionVerdict.Failed);
    }

    [Fact]
    public void Build_WhenPartHasSeveralAttempts_UsesLatestOne()
    {
        var older = Submitted(PartKind.Sluchanie, 5m);
        var newer = new ExamAttempt(Guid.NewGuid(), "student-1", _arkusz.Id, PartKind.Sluchanie, StartedAt.AddHours(2), SubmittedAt.AddHours(2), 25m, 30m, []);

        var actual = SessionScorer.Build(_arkusz, [older, newer]);

        actual.Parts[0].Points.Should().Be(25m);
    }
}
