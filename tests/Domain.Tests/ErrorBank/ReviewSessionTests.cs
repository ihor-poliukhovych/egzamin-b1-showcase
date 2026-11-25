using B1.Domain.ErrorBank;

namespace B1.Domain.Tests.ErrorBank;

public class ReviewSessionTests
{
    [Fact]
    public void Planned_items_keep_order_and_have_no_result()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var s = new ReviewSession("student-1", new DateOnly(2026, 10, 19), ids);
        Assert.Equal(ids, s.Items.Select(i => i.PatternId).ToArray());
        Assert.All(s.Items, i => Assert.Null(i.Result));
        Assert.False(s.IsComplete);
    }

    [Fact]
    public void Answering_all_items_completes_session()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var s = new ReviewSession("student-1", new DateOnly(2026, 10, 19), ids);
        s.Answer(ids[0], AttemptResult.Pass);
        s.Answer(ids[1], AttemptResult.Fail);
        Assert.True(s.IsComplete);
    }

    [Fact]
    public void Answer_for_unplanned_pattern_is_appended()
    {
        var s = new ReviewSession("student-1", new DateOnly(2026, 10, 19), [Guid.NewGuid()]);
        var extra = Guid.NewGuid();
        s.Answer(extra, AttemptResult.Fail);
        Assert.Equal(2, s.Items.Count);
        Assert.Equal(extra, s.Items[1].PatternId);
        Assert.Equal(1, s.Items[1].Order);
    }
}
