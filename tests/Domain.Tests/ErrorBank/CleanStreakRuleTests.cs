using B1.Domain.ErrorBank;
using static B1.Domain.Tests.ErrorBank.PatternFactory;

namespace B1.Domain.Tests.ErrorBank;

/// <summary>Rule: a pass counts toward cleanStreak only on a different day and with no fail for this pattern on the same day.</summary>
public class CleanStreakRuleTests
{
    [Fact]
    public void Pass_on_a_day_without_fail_counts()
    {
        var p = WithFalls(2); // falls on Day1, Day2
        p.Record(AttemptResult.Pass, Day(3));
        Assert.Equal(1, p.CleanStreak);
    }

    [Fact]
    public void Pass_on_the_same_day_as_fail_does_not_count()
    {
        var p = WithFalls(2); // last fall on Day2
        p.Record(AttemptResult.Pass, Day(2));
        Assert.Equal(0, p.CleanStreak);
    }

    [Fact]
    public void Second_pass_on_the_same_day_does_not_count_twice()
    {
        var p = WithFalls(2);
        p.Record(AttemptResult.Pass, Day(3));
        p.Record(AttemptResult.Pass, Day(3));
        Assert.Equal(1, p.CleanStreak);
    }

    [Fact]
    public void Passes_on_different_days_accumulate()
    {
        var p = WithFalls(2);
        p.Record(AttemptResult.Pass, Day(3));
        p.Record(AttemptResult.Pass, Day(4));
        Assert.Equal(2, p.CleanStreak);
    }

    [Fact]
    public void Pass_that_does_not_count_toward_streak_is_recorded_but_ignored()
    {
        var p = WithFalls(2);
        p.Record(AttemptResult.Pass, Day(3), AttemptSource.Mowienie, "z dyktowania", countsTowardStreak: false);
        Assert.Equal(0, p.CleanStreak);
        Assert.Contains(p.Attempts, a => a.Source == AttemptSource.Mowienie && !a.CountsTowardStreak);
    }

    [Fact]
    public void Every_attempt_is_kept_in_history()
    {
        var p = WithFalls(2);
        p.Record(AttemptResult.Pass, Day(2));
        p.Record(AttemptResult.Pass, Day(3), evidence: "rozmawiam z moją koleżanką");
        Assert.Equal(4, p.Attempts.Count);
        Assert.Equal("rozmawiam z moją koleżanką", p.Attempts.Last().Evidence);
    }
}
