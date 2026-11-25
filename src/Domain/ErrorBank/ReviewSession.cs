namespace B1.Domain.ErrorBank;

/// <summary>A user's daily blitz: which patterns are scheduled and how they were answered.</summary>
public class ReviewSession
{
    private readonly List<ReviewItem> _items = [];

    private ReviewSession() { }

    public ReviewSession(string userId, DateOnly date, IEnumerable<Guid> plannedPatternIds)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Date = date;
        var order = 0;
        foreach (var patternId in plannedPatternIds)
            _items.Add(new ReviewItem(Id, patternId, order++));
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public DateOnly Date { get; private set; }
    public int DurationSec { get; private set; }
    public IReadOnlyList<ReviewItem> Items => _items;

    public bool IsComplete => _items.All(i => i.Result is not null);

    /// <summary>Append new items to today's plan (for example, errors from the AI review), without exceeding the maximum.</summary>
    public int AddPlanned(IEnumerable<Guid> patternIds, int max)
    {
        var added = 0;
        foreach (var id in patternIds)
        {
            if (_items.Count >= max) break;
            if (_items.Any(i => i.PatternId == id)) continue;
            _items.Add(new ReviewItem(Id, id, _items.Count));
            added++;
        }
        return added;
    }

    /// <summary>Record an answer for a blitz item. An item that is not in the plan is appended to the end.</summary>
    public ReviewItem Answer(Guid patternId, AttemptResult result)
    {
        var item = _items.FirstOrDefault(i => i.PatternId == patternId);
        if (item is null)
        {
            item = new ReviewItem(Id, patternId, _items.Count);
            _items.Add(item);
        }
        item.SetResult(result);
        return item;
    }

    public void AddDuration(int seconds)
    {
        if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        DurationSec += seconds;
    }
}

public class ReviewItem
{
    private ReviewItem() { }

    internal ReviewItem(Guid sessionId, Guid patternId, int order)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        PatternId = patternId;
        Order = order;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid PatternId { get; private set; }
    public int Order { get; private set; }
    public AttemptResult? Result { get; private set; }

    internal void SetResult(AttemptResult result) => Result = result;
}
