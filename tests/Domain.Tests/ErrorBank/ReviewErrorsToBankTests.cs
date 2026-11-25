using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using AwesomeAssertions;

namespace B1.Domain.Tests.ErrorBank;

public class ReviewErrorsToBankTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 19, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 19);

    private static ReviewError Error(string fragment, string correct, string typ = "błąd gramatyczny") =>
        new(fragment, correct, typ, "Rule. Why it is an error. How to check.", "As in Russian", "b");

    [Fact]
    public void Apply_WhenNewErrors_CreatesPatternsWithFirstFallAndCategory()
    {
        var actual = ReviewErrorsToBank.Apply([], "student-1", "Pisanie 2024-06", [Error("prosze", "proszę", "błąd ortograficzny"), Error("o mojej pracy", "o moją pracę")],
            Today, AttemptSource.Pisanie, Now);

        actual.Should().HaveCount(2);
        actual[0].Category.Should().Be(PatternCategory.Ortografia);
        actual[1].Category.Should().Be(PatternCategory.Gramatyka);
        actual.Should().OnlyContain(p => p.FallsCount == 1 && p.UserId == "student-1" && p.Title.StartsWith("Pisanie 2024-06"));
    }

    [Fact]
    public void Apply_WhenSameErrorAlreadyInBank_CountsAnotherFallInsteadOfDuplicate()
    {
        var existing = new ErrorPattern("student-1", "Pisanie · prosze", "prosze", "proszę", "rule", PatternCategory.Ortografia, createdAt: Now.AddDays(-3));
        existing.Record(AttemptResult.Fail, Today.AddDays(-3), AttemptSource.Pisanie);

        var actual = ReviewErrorsToBank.Apply([existing], "student-1", "Pisanie 2024-06", [Error(" Prosze ", "proszę")], Today, AttemptSource.Pisanie, Now);

        actual.Should().BeEmpty();
        existing.FallsCount.Should().Be(2);
        existing.Status.Should().Be(PatternStatus.Active);
    }

    [Fact]
    public void Apply_WhenErrorRepeatsWithinOneReview_CreatesOnePattern()
    {
        var actual = ReviewErrorsToBank.Apply([], "student-1", "Pisanie", [Error("bedą", "będą"), Error("bedą", "będą")], Today, AttemptSource.Pisanie, Now);

        actual.Should().ContainSingle().Which.FallsCount.Should().Be(1);
    }

    [Fact]
    public void Apply_WhenFragmentOrCorrectionEmpty_SkipsError()
    {
        ReviewErrorsToBank.Apply([], "student-1", "Pisanie", [Error("", "x"), Error("x", " ")], Today, AttemptSource.Pisanie, Now).Should().BeEmpty();
    }

    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    public void LimitReached_WhenUsedToday_ComparesWithDailyLimit(int used, bool expected)
    {
        AiReviewUsage.LimitReached(used, 50).Should().Be(expected);
    }

    [Fact]
    public void DayStart_WhenLocalOffset_ReturnsUtcMidnight()
    {
        AiReviewUsage.DayStart(new DateTimeOffset(2026, 10, 20, 0, 30, 0, TimeSpan.FromHours(2)))
            .Should().Be(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
    }
}
