using B1.Domain.ErrorBank;
using static B1.Domain.Tests.ErrorBank.PatternFactory;

namespace B1.Domain.Tests.ErrorBank;

/// <summary>Rule: cleanStreak == 3 -> archived; any fail -> cleanStreak = 0, status = active.</summary>
public class ArchiveRuleTests
{
    [Fact]
    public void Two_clean_passes_keep_active()
    {
        var p = ActiveWithCleanStreak(2);
        Assert.Equal(PatternStatus.Active, p.Status);
    }

    [Fact]
    public void Three_clean_passes_archive()
    {
        var p = ActiveWithCleanStreak(3);
        Assert.Equal(PatternStatus.Archived, p.Status);
        Assert.Equal(3, p.CleanStreak);
    }

    [Fact]
    public void Fail_resets_clean_streak()
    {
        var p = ActiveWithCleanStreak(2);
        p.Record(AttemptResult.Fail, Day(10));
        Assert.Equal(0, p.CleanStreak);
    }

    [Fact]
    public void Fail_on_archived_pattern_returns_it_to_active()
    {
        var p = ActiveWithCleanStreak(3);
        p.Record(AttemptResult.Fail, Day(20));
        Assert.Equal(PatternStatus.Active, p.Status);
        Assert.Equal(0, p.CleanStreak);
        Assert.Equal(3, p.FallsCount);
    }

    [Fact]
    public void Streak_restarts_from_zero_after_fail()
    {
        var p = ActiveWithCleanStreak(2);
        p.Record(AttemptResult.Fail, Day(10));
        p.Record(AttemptResult.Pass, Day(11));
        p.Record(AttemptResult.Pass, Day(12));
        Assert.Equal(2, p.CleanStreak);
        Assert.Equal(PatternStatus.Active, p.Status);
    }
}
