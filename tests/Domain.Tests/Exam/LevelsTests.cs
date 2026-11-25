using B1.Domain.Exam;
using AwesomeAssertions;

namespace B1.Domain.Tests.Exam;

public class LevelsTests
{
    private static ExamPart Part(Guid arkuszId, PartKind kind, decimal? max = null)
    {
        var part = ExamPart.Create(arkuszId, kind, 40, []);
        part.MaxPointsOverride = max;
        return part;
    }

    [Fact]
    public void Create_WhenLevelNotGiven_DefaultsToB1Adult()
    {
        var arkusz = Arkusz.Create("2022-03", "marzec 2022", ArkuszSource.Official, []);

        arkusz.Level.Should().Be("B1");
        arkusz.Variant.Should().Be("dorosli");
    }

    [Fact]
    public void Create_WhenLevelAndVariantGiven_KeepsThem()
    {
        var arkusz = Arkusz.Create("dim-B2-2022-06", "B2 młodzież", ArkuszSource.Official, [], "B2", "dim");

        arkusz.Level.Should().Be("B2");
        arkusz.Variant.Should().Be("dim");
    }

    [Theory]
    [InlineData(PartKind.Pisanie, null, 30)]
    [InlineData(PartKind.Mowienie, null, 40)]
    [InlineData(PartKind.Pisanie, 40, 40)]
    [InlineData(PartKind.Mowienie, 30, 30)]
    public void MaxPoints_WhenOverrideGiven_UsesItElseLevelDefault(PartKind kind, int? over, int expected)
    {
        var part = Part(Guid.NewGuid(), kind, over);

        part.MaxPoints.Should().Be(expected);
    }

    [Fact]
    public void Build_WhenArkuszHasNoSpeakingPart_SessionEndsAfterWritingAndCanBePassed()
    {
        var arkusz = Arkusz.Create("B2-2022-06", "B2", ArkuszSource.Official, [], "B2", "dorosli");
        arkusz.Parts = new[] { PartKind.Sluchanie, PartKind.Czytanie, PartKind.Gramatyka, PartKind.Pisanie }.Select(k => Part(arkusz.Id, k, 40)).ToList();
        var attempts = arkusz.Parts
            .Select(p => new ExamAttempt(Guid.NewGuid(), "u", arkusz.Id, p.Kind, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 24m, 40m, []))
            .ToList();

        var actual = SessionScorer.Build(arkusz, attempts);

        actual.Parts.Should().HaveCount(4);
        actual.NextPart.Should().BeNull();
        actual.Verdict.Should().Be(SessionVerdict.Passed);
    }

    [Fact]
    public void Build_WhenPartMaxIsFortyAndScoreIsNineteen_IsBelowThreshold()
    {
        var arkusz = Arkusz.Create("B2-2022-06", "B2", ArkuszSource.Official, [], "B2", "dorosli");
        arkusz.Parts = [Part(arkusz.Id, PartKind.Sluchanie, 40)];
        var attempt = new ExamAttempt(Guid.NewGuid(), "u", arkusz.Id, PartKind.Sluchanie, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 19m, 40m, []);

        var actual = SessionScorer.Build(arkusz, [attempt]);

        actual.Parts[0].Percent.Should().Be(47.5m);
        actual.Parts[0].IsBelowThreshold.Should().BeTrue();
    }
}

public class ShortAnswerTests
{
    private static (ExamTask Task, ExamItem Full, ExamItem Half) Build()
    {
        var task = ExamTask.Create(Guid.NewGuid(), 5, ExamTaskType.ShortAnswer, "Proszę odpowiedzieć na pytania.", 1m, null, null, null, [], []);
        var full = ExamItem.Create(task.Id, 1, "Jaki jest metraż?", null, [], ["27 metrów kwadratowych", "27 m2"], null);
        var half = ExamItem.Create(task.Id, 2, "Na jakie sprzęty?", null, [], ["zmywarkę"], null);
        half.PointsOverride = 0.5m;
        task.Items = [full, half];
        return (task, full, half);
    }

    [Fact]
    public void MaxPoints_WhenItemHasOverride_SumsEffectivePoints()
    {
        var (task, _, _) = Build();

        task.MaxPoints.Should().Be(1.5m);
    }

    [Fact]
    public void GradeAnswer_WhenShortAnswerMatchesAnyVariant_IsCorrect()
    {
        var (task, full, _) = Build();

        ExamGrader.GradeAnswer(task, full, "27 M2.").Should().Be(AnswerVerdict.Correct);
    }

    [Fact]
    public void GradeAnswer_WhenShortAnswerDiffers_NeedsReviewInsteadOfWrong()
    {
        var (task, full, _) = Build();

        ExamGrader.GradeAnswer(task, full, "dwadzieścia siedem metrów").Should().Be(AnswerVerdict.NeedsReview);
    }

    [Fact]
    public void Submit_WhenHalfPointItemCorrect_AwardsItsOwnPointsAndAcceptUsesThem()
    {
        var arkusz = Arkusz.Create("B2-x", "B2", ArkuszSource.Official, [], "B2", "dorosli");
        var (task, full, half) = Build();
        var part = ExamPart.Create(arkusz.Id, PartKind.Sluchanie, 40, [task]);
        task.PartId = part.Id;
        arkusz.Parts = [part];
        var attempt = ExamAttempt.Create("u", arkusz, part, DateTimeOffset.UtcNow);
        attempt.SaveAnswer(half.Id, "zmywarkę", DateTimeOffset.UtcNow);
        attempt.SaveAnswer(full.Id, "inaczej", DateTimeOffset.UtcNow);

        ExamGrader.Submit(attempt, part, DateTimeOffset.UtcNow);
        attempt.Points.Should().Be(0.5m);

        ExamGrader.Accept(attempt, part, full.Id);
        attempt.Points.Should().Be(1.5m);
    }
}

public class FullExamTests
{
    [Fact]
    public void IsFullExam_WhenOnlySomePartsPublished_IsFalse()
    {
        var arkusz = Arkusz.Create("B2-2022-06", "B2", ArkuszSource.Official, [], "B2", "dorosli");
        arkusz.Parts = [ExamPart.Create(arkusz.Id, PartKind.Sluchanie, 40, [])];

        arkusz.IsFullExam.Should().BeFalse();
    }

    [Fact]
    public void IsFullExam_WhenAllCorePartsPresent_IsTrueRegardlessOfSpeaking()
    {
        var arkusz = Arkusz.Create("B2-2022-06", "B2", ArkuszSource.Official, [], "B2", "dorosli");
        arkusz.Parts = Arkusz.CoreParts.Select(k => ExamPart.Create(arkusz.Id, k, 40, [])).ToList();

        arkusz.IsFullExam.Should().BeTrue();
    }
}
