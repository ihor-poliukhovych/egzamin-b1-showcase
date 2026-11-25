using B1.Domain.ErrorBank;
using static B1.Domain.Tests.ErrorBank.PatternFactory;

namespace B1.Domain.Tests.ErrorBank;

/// <summary>Rule: nextDueAt intervals 1 -> 3 -> 7 days after clean runs; after a fall: tomorrow.</summary>
public class IntervalRuleTests
{
    [Fact]
    public void After_fail_next_due_is_tomorrow()
    {
        var p = WithFalls(2); // last fall on Day2
        Assert.Equal(Day(3), p.NextDueAt);
    }

    [Fact]
    public void After_first_clean_pass_next_due_in_1_day()
    {
        var p = WithFalls(2);
        p.Record(AttemptResult.Pass, Day(3));
        Assert.Equal(Day(4), p.NextDueAt);
    }

    [Fact]
    public void After_second_clean_pass_next_due_in_3_days()
    {
        var p = WithFalls(2);
        p.Record(AttemptResult.Pass, Day(3));
        p.Record(AttemptResult.Pass, Day(4));
        Assert.Equal(Day(7), p.NextDueAt);
    }

    [Fact]
    public void After_third_clean_pass_next_due_in_7_days()
    {
        var p = WithFalls(2);
        p.Record(AttemptResult.Pass, Day(3));
        p.Record(AttemptResult.Pass, Day(4));
        p.Record(AttemptResult.Pass, Day(7));
        Assert.Equal(Day(14), p.NextDueAt);
    }

    [Fact]
    public void Fail_after_clean_passes_brings_due_back_to_tomorrow()
    {
        var p = ActiveWithCleanStreak(2);
        p.Record(AttemptResult.Fail, Day(10));
        Assert.Equal(Day(11), p.NextDueAt);
    }

    [Fact]
    public void Non_counting_pass_does_not_move_due_date()
    {
        var p = WithFalls(2);
        var before = p.NextDueAt;
        p.Record(AttemptResult.Pass, Day(2)); // on the day of the fall: not counted
        Assert.Equal(before, p.NextDueAt);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 7)]
    [InlineData(5, 7)]
    public void Policy_interval_table(int streak, int expectedDays)
    {
        Assert.Equal(expectedDays, ReviewPolicy.IntervalAfterStreak(streak));
    }
}
