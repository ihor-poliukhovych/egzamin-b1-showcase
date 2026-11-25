namespace B1.Domain.ErrorBank;

/// <summary>One attempt at a pattern: passed / failed, where it came from and what was written.</summary>
public class Attempt
{
    private Attempt() { }

    internal Attempt(Guid patternId, DateOnly date, AttemptResult result, AttemptSource source, string? evidence, bool countsTowardStreak)
    {
        Id = Guid.NewGuid();
        PatternId = patternId;
        Date = date;
        Result = result;
        Source = source;
        Evidence = evidence;
        CountsTowardStreak = countsTowardStreak;
    }

    public Guid Id { get; private set; }
    public Guid PatternId { get; private set; }
    public DateOnly Date { get; private set; }
    public AttemptResult Result { get; private set; }
    public AttemptSource Source { get; private set; }
    public string? Evidence { get; private set; }

    /// <summary>false, for example for a clean form from voice dictation: recorded, but not counted toward cleanStreak.</summary>
    public bool CountsTowardStreak { get; private set; }
}
