using B1.Domain.ErrorBank;
using static B1.Domain.Tests.ErrorBank.PatternFactory;

namespace B1.Domain.Tests.ErrorBank;

/// <summary>Rule: 2+ falls -> active.</summary>
public class ActivationRuleTests
{
    [Fact]
    public void New_pattern_starts_as_watch()
    {
        var p = New();
        Assert.Equal(PatternStatus.Watch, p.Status);
        Assert.Equal(0, p.FallsCount);
    }

    [Fact]
    public void One_fall_keeps_watch()
    {
        var p = WithFalls(1);
        Assert.Equal(PatternStatus.Watch, p.Status);
        Assert.Equal(1, p.FallsCount);
    }

    [Fact]
    public void Two_falls_make_active()
    {
        var p = WithFalls(2);
        Assert.Equal(PatternStatus.Active, p.Status);
        Assert.Equal(2, p.FallsCount);
    }

    [Fact]
    public void Fall_increments_falls_count_each_time()
    {
        var p = WithFalls(5);
        Assert.Equal(5, p.FallsCount);
    }

    [Fact]
    public void Active_patterns_sort_by_falls_desc()
    {
        var patterns = new[] { WithFalls(2, "A"), WithFalls(7, "B"), WithFalls(4, "C") };
        var blitz = BlitzSelector.Select(patterns, Day(30));
        Assert.Equal(["B", "C", "A"], blitz.Select(p => p.Code!).ToArray());
    }
}
