namespace B1.Domain.Exam;

/// <summary>What a model call was spent on; each kind has its own daily limit.</summary>
public enum AiUsageKind
{
    /// <summary>Pisanie or Mowienie review on /finish.</summary>
    Review = 0,

    /// <summary>One adapted line of the Mowienie task 3 conversation partner.</summary>
    DialogTurn = 1,

    /// <summary>One batch of the error grouper on GET /today.</summary>
    Grouping = 2,
}

/// <summary>
/// One model call reserved for a user, used for the daily limits. The row is written before the model is called and kept
/// whatever the outcome (failure, parse error, cancellation), so retries cannot bypass the limit.
/// </summary>
public class AiReviewUsage
{
    protected AiReviewUsage() { }

    public AiReviewUsage(Guid id, string userId, Guid attemptId, PartKind part, DateTimeOffset at, AiUsageKind kind = AiUsageKind.Review)
    {
        Id = id;
        UserId = userId;
        AttemptId = attemptId;
        Part = part;
        At = at;
        Kind = kind;
    }

    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>The attempt the call belongs to; <see cref="Guid.Empty"/> for calls without an attempt (grouping).</summary>
    public Guid AttemptId { get; set; }

    /// <summary>The exam part; <see cref="PartKind.None"/> for calls without a part (grouping).</summary>
    public PartKind Part { get; set; }

    public DateTimeOffset At { get; set; }
    public AiUsageKind Kind { get; set; }

    public static AiReviewUsage Create(string userId, AiUsageKind kind, Guid attemptId, PartKind part, DateTimeOffset at) =>
        new(Guid.NewGuid(), userId, attemptId, part, at, kind);

    /// <summary>Start of the current UTC day: the limit is counted from it.</summary>
    public static DateTimeOffset DayStart(DateTimeOffset now) => new(now.UtcDateTime.Date, TimeSpan.Zero);

    public static bool LimitReached(int usedToday, int dailyLimit) => usedToday >= dailyLimit;
}
