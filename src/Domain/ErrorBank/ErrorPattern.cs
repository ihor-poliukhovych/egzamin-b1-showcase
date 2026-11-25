namespace B1.Domain.ErrorBank;

/// <summary>
/// A relapse is a user's recurring error. Holds the counters and applies the rules itself for
/// activation, clean runs, archiving and repetition intervals.
/// </summary>
public class ErrorPattern
{
    private readonly List<Attempt> _attempts = [];

    private ErrorPattern() { }

    public ErrorPattern(
        string userId,
        string title,
        string wrongExample,
        string correctForm,
        string rule,
        PatternCategory category,
        string? contrastRuUa = null,
        string? code = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required", nameof(userId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title is required", nameof(title));

        Id = Guid.NewGuid();
        UserId = userId;
        Code = code;
        Title = title;
        WrongExample = wrongExample;
        CorrectForm = correctForm;
        Rule = rule;
        ContrastRuUa = contrastRuUa;
        Category = category;
        Status = PatternStatus.Watch;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;

    /// <summary>Human-readable code from the MVP (R23), for seed and references; optional.</summary>
    public string? Code { get; private set; }

    public string Title { get; private set; } = default!;

    /// <summary>Review step 1: what was written.</summary>
    public string WrongExample { get; private set; } = default!;

    /// <summary>Review step 2: what is correct.</summary>
    public string CorrectForm { get; private set; } = default!;

    /// <summary>Review step 3: the rule and why the error happens, with a self-check trick.</summary>
    public string Rule { get; private set; } = default!;

    /// <summary>Contrast with Russian/Ukrainian.</summary>
    public string? ContrastRuUa { get; private set; }

    public PatternCategory Category { get; private set; }

    /// <summary>Error type assigned by the AI grouper ("Dopełniacz l.mn.", "Ogonki ę/ą"); null means not grouped yet.</summary>
    public string? GroupName { get; private set; }

    public PatternStatus Status { get; private set; }
    public int FallsCount { get; private set; }
    public int CleanStreak { get; private set; }
    public DateOnly? NextDueAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Date of the last fall: needed by the rule "a pass on the day of a fall does not count".</summary>
    public DateOnly? LastFailDate { get; private set; }

    /// <summary>Date of the last pass counted toward cleanStreak: needed by the rule "one clean run per day".</summary>
    public DateOnly? LastCleanPassDate { get; private set; }

    public IReadOnlyCollection<Attempt> Attempts => _attempts;

    public void Update(string title, string wrongExample, string correctForm, string rule, PatternCategory category, string? contrastRuUa, string? code)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title is required", nameof(title));
        Title = title;
        WrongExample = wrongExample;
        CorrectForm = correctForm;
        Rule = rule;
        Category = category;
        ContrastRuUa = contrastRuUa;
        Code = code;
    }

    /// <summary>The AI explained the rule for an error that had only the task context: "explanation\nZadanie I: „…”".</summary>
    public void ExplainRule(string explanation)
    {
        var text = explanation.Trim();
        if (text.Length == 0 || !Rule.StartsWith(ErrorGrouping.ContextRulePrefix, StringComparison.Ordinal)) return;
        Rule = $"{(text.Length > MaxExplanationLength ? text[..MaxExplanationLength] : text)}\n{Rule}";
    }

    private const int MaxExplanationLength = 300;

    public void AssignGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentException("groupName is required", nameof(groupName));
        var name = groupName.Trim();
        GroupName = name.Length > ErrorGrouping.MaxGroupNameLength ? name[..ErrorGrouping.MaxGroupNameLength] : name;
    }

    /// <summary>
    /// Record an attempt and apply the rules.
    /// Fail: fallsCount++, cleanStreak = 0, with 2+ falls -> active, nextDueAt = tomorrow.
    /// Pass: counts toward cleanStreak only if countsTowardStreak, there was no fail on that day
    /// and the previous counted pass was on a different day. cleanStreak == 3 -> archived.
    /// nextDueAt after a clean run: 1 -> 3 -> 7 days.
    /// </summary>
    public Attempt Record(AttemptResult result, DateOnly date, AttemptSource source = AttemptSource.Blitz, string? evidence = null, bool countsTowardStreak = true)
    {
        var attempt = new Attempt(Id, date, result, source, evidence, countsTowardStreak);
        _attempts.Add(attempt);

        if (result == AttemptResult.Fail)
            ApplyFail(date);
        else if (countsTowardStreak && IsCleanPassCountable(date))
            ApplyCleanPass(date);

        return attempt;
    }

    private bool IsCleanPassCountable(DateOnly date)
    {
        if (LastFailDate == date) return false;
        if (LastCleanPassDate == date) return false;
        return true;
    }

    private void ApplyFail(DateOnly date)
    {
        FallsCount++;
        CleanStreak = 0;
        LastFailDate = date;
        if (FallsCount >= ReviewPolicy.FallsToActivate)
            Status = PatternStatus.Active;
        NextDueAt = date.AddDays(ReviewPolicy.DaysAfterFail);
    }

    private void ApplyCleanPass(DateOnly date)
    {
        CleanStreak++;
        LastCleanPassDate = date;
        if (CleanStreak >= ReviewPolicy.CleanStreakToArchive)
            Status = PatternStatus.Archived;
        NextDueAt = date.AddDays(ReviewPolicy.IntervalAfterStreak(CleanStreak));
    }

    /// <summary>
    /// Restore counters from an external source (import from the MVP). Status and nextDueAt
    /// are derived by the same rules as for live attempts.
    /// </summary>
    public void RestoreCounters(int fallsCount, int cleanStreak, DateOnly? lastFailDate, DateOnly? lastCleanPassDate)
    {
        if (fallsCount < 0) throw new ArgumentOutOfRangeException(nameof(fallsCount));
        if (cleanStreak < 0) throw new ArgumentOutOfRangeException(nameof(cleanStreak));

        FallsCount = fallsCount;
        CleanStreak = cleanStreak;
        LastFailDate = lastFailDate;
        LastCleanPassDate = lastCleanPassDate;

        Status = cleanStreak >= ReviewPolicy.CleanStreakToArchive ? PatternStatus.Archived
            : fallsCount >= ReviewPolicy.FallsToActivate ? PatternStatus.Active
            : PatternStatus.Watch;

        var anchor = cleanStreak > 0 ? lastCleanPassDate : lastFailDate;
        NextDueAt = anchor?.AddDays(ReviewPolicy.IntervalAfterStreak(cleanStreak));
    }
}
