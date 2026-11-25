using B1.Domain.ErrorBank;
using static B1.Domain.Tests.ErrorBank.PatternFactory;

namespace B1.Domain.Tests.ErrorBank;

/// <summary>Rule: blitz of 5-8 items: first due by nextDueAt, then the most frequent, 1-2 from the archive as a check.</summary>
public class BlitzSelectorTests
{
    private static readonly DateOnly Today = Day(30);

    /// <summary>An active pattern with a given number of falls and nextDueAt (via clean runs or a fall).</summary>
    private static ErrorPattern Active(string code, int falls, DateOnly lastFall)
    {
        var p = New(code: code, title: code);
        for (var i = 0; i < falls - 1; i++)
            p.Record(AttemptResult.Fail, Day(1));
        p.Record(AttemptResult.Fail, lastFall); // nextDueAt = lastFall + 1
        return p;
    }

    private static ErrorPattern Archived(string code) => ActiveWithCleanStreak(3, code);

    [Fact]
    public void Returns_at_most_8_items()
    {
        var patterns = Enumerable.Range(1, 20).Select(i => Active($"P{i}", falls: i + 1, lastFall: Day(5)));
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal(8, blitz.Count);
    }

    [Fact]
    public void Due_items_come_first_most_frequent_on_top()
    {
        var patterns = new[]
        {
            Active("due-low", falls: 2, lastFall: Day(5)),
            Active("due-high", falls: 9, lastFall: Day(5)),
            Active("not-due", falls: 20, lastFall: Today), // nextDueAt = tomorrow
        };
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal("due-high", blitz[0].Code);
        Assert.Equal("due-low", blitz[1].Code);
    }

    [Fact]
    public void Fills_up_to_minimum_with_most_frequent_non_due_items()
    {
        var patterns = new[]
        {
            Active("due", falls: 2, lastFall: Day(5)),
            Active("nd-3", falls: 3, lastFall: Today),
            Active("nd-9", falls: 9, lastFall: Today),
            Active("nd-5", falls: 5, lastFall: Today),
            Active("nd-4", falls: 4, lastFall: Today),
            Active("nd-2", falls: 2, lastFall: Today),
        };
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal(5, blitz.Count);
        Assert.Equal(["due", "nd-9", "nd-5", "nd-4", "nd-3"], blitz.Select(p => p.Code!).ToArray());
    }

    [Fact]
    public void Does_not_add_non_due_items_beyond_minimum()
    {
        var patterns = Enumerable.Range(1, 6).Select(i => Active($"due{i}", falls: 2, lastFall: Day(5)))
            .Concat(Enumerable.Range(1, 6).Select(i => Active($"nd{i}", falls: 50, lastFall: Today)));
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal(6, blitz.Count);
        Assert.All(blitz, p => Assert.StartsWith("due", p.Code));
    }

    [Fact]
    public void Includes_one_or_two_archived_items_for_control()
    {
        var patterns = Enumerable.Range(1, 10).Select(i => Active($"due{i}", falls: 2, lastFall: Day(5)))
            .Concat([Archived("arch1"), Archived("arch2"), Archived("arch3")]);
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal(8, blitz.Count);
        Assert.Equal(2, blitz.Count(p => p.Status == PatternStatus.Archived));
        Assert.Equal(6, blitz.Count(p => p.Status == PatternStatus.Active));
    }

    [Fact]
    public void Single_archived_item_is_included_when_only_one_exists()
    {
        var patterns = Enumerable.Range(1, 10).Select(i => Active($"due{i}", falls: 2, lastFall: Day(5)))
            .Concat([Archived("arch1")]);
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Single(blitz, p => p.Status == PatternStatus.Archived);
    }

    [Fact]
    public void Fresh_watch_patterns_fill_remaining_room_after_active()
    {
        var patterns = new[] { WithFalls(1, "watch"), Active("active", 2, Day(5)) };
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal(["active", "watch"], blitz.Select(p => p.Code));
    }

    [Fact]
    public void Fresh_watch_patterns_do_not_push_out_active_ones()
    {
        var patterns = Enumerable.Range(1, 8).Select(i => Active($"due{i}", falls: 2, lastFall: Day(5)))
            .Append(WithFalls(1, "watch"));
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.DoesNotContain(blitz, p => p.Code == "watch");
    }

    [Fact]
    public void Returns_fewer_than_minimum_when_bank_is_small()
    {
        var patterns = new[] { Active("a", 2, Day(5)), Active("b", 3, Day(5)) };
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal(2, blitz.Count);
    }

    [Fact]
    public void Archived_items_go_last()
    {
        var patterns = new[] { Archived("arch"), Active("a", 2, Day(5)) };
        var blitz = BlitzSelector.Select(patterns, Today);
        Assert.Equal(["a", "arch"], blitz.Select(p => p.Code!).ToArray());
    }
}
