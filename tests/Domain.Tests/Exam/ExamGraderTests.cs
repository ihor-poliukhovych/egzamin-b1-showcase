using B1.Domain.Exam;
using AwesomeAssertions;
using static B1.Domain.Tests.Exam.ArkuszFactory;

namespace B1.Domain.Tests.Exam;

public class ExamGraderTests
{
    private readonly Arkusz _arkusz;
    private readonly ExamPart _part;
    private readonly ExamTask _chooseTask;
    private readonly ExamTask _transformTask;
    private readonly ExamAttempt _attempt;

    public ExamGraderTests()
    {
        _arkusz = TwoTaskArkusz();
        _part = _arkusz.Parts[0];
        _chooseTask = _part.Tasks[0];
        _transformTask = _part.Tasks[1];
        _attempt = AttemptFor(_arkusz);
    }

    [Fact]
    public void GradeAnswer_WhenChoiceMatchesKey_ReturnsCorrect()
    {
        var actual = ExamGrader.GradeAnswer(_chooseTask, _chooseTask.Items[0], "tą");

        actual.Should().Be(AnswerVerdict.Correct);
    }

    [Fact]
    public void GradeAnswer_WhenChoiceDoesNotMatchKey_ReturnsWrong()
    {
        var actual = ExamGrader.GradeAnswer(_chooseTask, _chooseTask.Items[0], "tę");

        actual.Should().Be(AnswerVerdict.Wrong);
    }

    [Fact]
    public void GradeAnswer_WhenWordBankChoiceMatchesKey_ReturnsCorrect()
    {
        var task = WordBankTask(_part.Id);

        var actual = ExamGrader.GradeAnswer(task, task.Items[0], "ponieważ");

        actual.Should().Be(AnswerVerdict.Correct);
    }

    [Fact]
    public void GradeAnswer_WhenFreeTextMatchesAlternativeKey_ReturnsCorrect()
    {
        var actual = ExamGrader.GradeAnswer(_transformTask, _transformTask.Items[0], "Brat dostał książkę od Oli.");

        actual.Should().Be(AnswerVerdict.Correct);
    }

    [Fact]
    public void GradeAnswer_WhenFreeTextDoesNotMatch_ReturnsNeedsReview()
    {
        var actual = ExamGrader.GradeAnswer(_transformTask, _transformTask.Items[0], "Brat otrzymał książkę od Oli.");

        actual.Should().Be(AnswerVerdict.NeedsReview);
    }

    [Fact]
    public void GradeAnswer_WhenFreeTextIsEmpty_ReturnsWrong()
    {
        var actual = ExamGrader.GradeAnswer(_transformTask, _transformTask.Items[0], "   ");

        actual.Should().Be(AnswerVerdict.Wrong);
    }

    [Theory]
    [InlineData(AnswerVerdict.Correct, 0.5, 0.5)]
    [InlineData(AnswerVerdict.Accepted, 1, 1)]
    [InlineData(AnswerVerdict.Wrong, 1, 0)]
    [InlineData(AnswerVerdict.NeedsReview, 1, 0)]
    [InlineData(AnswerVerdict.None, 1, 0)]
    public void PointsFor_WhenVerdictGiven_ReturnsPointsOnlyForCorrectOrAccepted(AnswerVerdict verdict, decimal pointsPerItem, decimal expected)
    {
        var actual = ExamGrader.PointsFor(verdict, pointsPerItem);

        actual.Should().Be(expected);
    }

    [Fact]
    public void Submit_WhenAllAnswersCorrect_SetsFullScoreAndSubmittedAt()
    {
        // Arrange
        _attempt.SaveAnswer(_chooseTask.Items[0].Id, "tą", StartedAt);
        _attempt.SaveAnswer(_chooseTask.Items[1].Id, "tą", StartedAt);
        _attempt.SaveAnswer(_transformTask.Items[0].Id, "Brat dostał od Oli książkę", StartedAt);
        _attempt.SaveAnswer(_transformTask.Items[1].Id, "Nie pamiętam, jaki jest adres tego hotelu", StartedAt);

        // Act
        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        // Assert
        _attempt.SubmittedAt.Should().Be(SubmittedAt);
        _attempt.Points.Should().Be(3m);
        _attempt.MaxPoints.Should().Be(3m);
        _attempt.Answers.Should().OnlyContain(a => a.Verdict == AnswerVerdict.Correct);
    }

    [Fact]
    public void Submit_WhenSomeItemsUnanswered_CreatesWrongAnswersForThem()
    {
        _attempt.SaveAnswer(_chooseTask.Items[0].Id, "tą", StartedAt);

        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        _attempt.Answers.Should().HaveCount(4);
        _attempt.Points.Should().Be(0.5m);
        _attempt.FindAnswer(_chooseTask.Items[1].Id)!.Verdict.Should().Be(AnswerVerdict.Wrong);
    }

    [Fact]
    public void Submit_WhenFreeTextDoesNotMatch_MarksNeedsReviewWithZeroPoints()
    {
        _attempt.SaveAnswer(_transformTask.Items[0].Id, "Brat otrzymał książkę od Oli", StartedAt);

        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        var answer = _attempt.FindAnswer(_transformTask.Items[0].Id)!;
        answer.Verdict.Should().Be(AnswerVerdict.NeedsReview);
        answer.Points.Should().Be(0m);
    }

    [Fact]
    public void Submit_WhenAlreadySubmitted_Throws()
    {
        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        var act = () => ExamGrader.Submit(_attempt, _part, SubmittedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Submit_WhenAttemptBelongsToAnotherArkusz_Throws()
    {
        var other = TwoTaskArkusz();

        var act = () => ExamGrader.Submit(_attempt, other.Parts[0], SubmittedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Accept_WhenAnswerNeedsReview_GrantsFullItemPointsAndRecalculatesTotal()
    {
        // Arrange
        _attempt.SaveAnswer(_chooseTask.Items[0].Id, "tą", StartedAt);
        _attempt.SaveAnswer(_transformTask.Items[0].Id, "Brat otrzymał książkę od Oli", StartedAt);
        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        // Act
        var accepted = ExamGrader.Accept(_attempt, _part, _transformTask.Items[0].Id);

        // Assert
        var answer = _attempt.FindAnswer(_transformTask.Items[0].Id)!;
        accepted.Should().BeTrue();
        answer.Verdict.Should().Be(AnswerVerdict.Accepted);
        answer.Points.Should().Be(1m);
        _attempt.Points.Should().Be(1.5m);
    }

    [Fact]
    public void Accept_WhenAnswerAlreadyCorrect_ReturnsFalseAndDoesNotChangeAnything()
    {
        _attempt.SaveAnswer(_chooseTask.Items[0].Id, "tą", StartedAt);
        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        var accepted = ExamGrader.Accept(_attempt, _part, _chooseTask.Items[0].Id);

        accepted.Should().BeFalse();
        _attempt.FindAnswer(_chooseTask.Items[0].Id)!.Verdict.Should().Be(AnswerVerdict.Correct);
        _attempt.Points.Should().Be(0.5m);
    }

    [Fact]
    public void Accept_WhenAnswerWrongInClosedTask_ReturnsFalseAndKeepsZeroPoints()
    {
        _attempt.SaveAnswer(_chooseTask.Items[0].Id, "tę", StartedAt);
        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        var accepted = ExamGrader.Accept(_attempt, _part, _chooseTask.Items[0].Id);

        accepted.Should().BeFalse();
        _attempt.FindAnswer(_chooseTask.Items[0].Id)!.Verdict.Should().Be(AnswerVerdict.Wrong);
        _attempt.Points.Should().Be(0m);
    }

    [Fact]
    public void Accept_WhenAttemptNotSubmitted_Throws()
    {
        var act = () => ExamGrader.Accept(_attempt, _part, _chooseTask.Items[0].Id);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Score_WhenSubmitted_BreaksDownByTaskAndComputesPercent()
    {
        // Arrange
        _attempt.SaveAnswer(_chooseTask.Items[0].Id, "tą", StartedAt);
        _attempt.SaveAnswer(_chooseTask.Items[1].Id, "tę", StartedAt);
        _attempt.SaveAnswer(_transformTask.Items[0].Id, "Brat dostał książkę od Oli", StartedAt);
        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        // Act
        var actual = ExamGrader.Score(_attempt, _part);

        // Assert
        var expected = new ExamScore(1.5m, 3m,
        [
            new TaskScore(1, "I", 0.5m, 1m),
            new TaskScore(6, "VI", 1m, 2m),
        ]);
        actual.Should().BeEquivalentTo(expected);
        actual.Percent.Should().Be(50m);
        actual.IsPassed.Should().BeTrue();
    }

    [Fact]
    public void Score_WhenBelowHalf_IsNotPassed()
    {
        _attempt.SaveAnswer(_chooseTask.Items[0].Id, "tą", StartedAt);
        ExamGrader.Submit(_attempt, _part, SubmittedAt);

        var actual = ExamGrader.Score(_attempt, _part);

        actual.Percent.Should().Be(16.7m);
        actual.IsPassed.Should().BeFalse();
    }

    [Fact]
    public void Score_WhenArkuszHasNoTasks_ReturnsZeroWithoutDivisionByZero()
    {
        var empty = Arkusz.Create("own-1", "własny", ArkuszSource.Own, []);
        empty.Parts = [ExamPart.Create(empty.Id, PartKind.Gramatyka, 45, [])];
        var attempt = AttemptFor(empty);

        var actual = ExamGrader.Score(attempt, empty.Parts[0]);

        actual.Percent.Should().Be(0m);
        actual.IsPassed.Should().BeFalse();
    }
}

public class ExamGraderCzytanieTests
{
    [Theory]
    [InlineData(ExamTaskType.MultipleChoice, "c) autobus odjeżdża o ósmej rano.", "c) autobus odjeżdża o ósmej rano.", AnswerVerdict.Correct)]
    [InlineData(ExamTaskType.MultipleChoice, "a) sklep jest zamknięty w niedzielę.", "c) autobus odjeżdża o ósmej rano.", AnswerVerdict.Wrong)]
    [InlineData(ExamTaskType.TrueFalse, "TAK", "TAK", AnswerVerdict.Correct)]
    [InlineData(ExamTaskType.TrueFalse, "NIE", "TAK", AnswerVerdict.Wrong)]
    [InlineData(ExamTaskType.Matching, "G. jak spędzić weekend w górach", "G. jak spędzić weekend w górach", AnswerVerdict.Correct)]
    [InlineData(ExamTaskType.Matching, "H. dlaczego warto wstawać wcześnie", "G. jak spędzić weekend w górach", AnswerVerdict.Wrong)]
    public void GradeAnswer_WhenCzytanieTaskType_ComparesChosenOptionWithKeyAndNeverNeedsReview(ExamTaskType type, string value, string key, AnswerVerdict expected)
    {
        var task = ExamTask.Create(Guid.NewGuid(), 1, type, "Proszę zaznaczyć.", 1m, null, null, null, [], []);
        var item = ExamItem.Create(task.Id, 1, "prompt", null, [], [key], null);

        var actual = ExamGrader.GradeAnswer(task, item, value);

        actual.Should().Be(expected);
    }
}
