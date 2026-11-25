using B1.Domain.ErrorBank;
using static B1.Domain.Tests.ErrorBank.PatternFactory;

namespace B1.Domain.Tests.ErrorBank;

/// <summary>Import from the MVP: status and nextDueAt are derived from the counters by the same rules.</summary>
public class RestoreCountersTests
{
    [Fact]
    public void Falls_2_streak_0_gives_active_due_day_after_fail()
    {
        var p = New();
        p.RestoreCounters(fallsCount: 2, cleanStreak: 0, lastFailDate: Day(10), lastCleanPassDate: null);
        Assert.Equal(PatternStatus.Active, p.Status);
        Assert.Equal(Day(11), p.NextDueAt);
    }

    [Fact]
    public void Streak_2_gives_due_3_days_after_last_clean_pass()
    {
        var p = New();
        p.RestoreCounters(fallsCount: 4, cleanStreak: 2, lastFailDate: Day(5), lastCleanPassDate: Day(9));
        Assert.Equal(PatternStatus.Active, p.Status);
        Assert.Equal(Day(12), p.NextDueAt);
    }

    [Fact]
    public void Streak_3_gives_archived()
    {
        var p = New();
        p.RestoreCounters(fallsCount: 5, cleanStreak: 3, lastFailDate: Day(5), lastCleanPassDate: Day(8));
        Assert.Equal(PatternStatus.Archived, p.Status);
        Assert.Equal(Day(15), p.NextDueAt);
    }

    [Fact]
    public void Falls_1_gives_watch()
    {
        var p = New();
        p.RestoreCounters(fallsCount: 1, cleanStreak: 0, lastFailDate: Day(7), lastCleanPassDate: null);
        Assert.Equal(PatternStatus.Watch, p.Status);
    }

    [Fact]
    public void Restored_pattern_continues_to_apply_rules()
    {
        var p = New();
        p.RestoreCounters(fallsCount: 3, cleanStreak: 2, lastFailDate: Day(5), lastCleanPassDate: Day(9));
        p.Record(AttemptResult.Pass, Day(12));
        Assert.Equal(PatternStatus.Archived, p.Status);
    }
}
