namespace B1.Domain.ErrorBank;

/// <summary>Numeric spaced-repetition rules from koncepcja.md (section "Core data model").</summary>
public static class ReviewPolicy
{
    /// <summary>This many falls make a pattern an active relapse.</summary>
    public const int FallsToActivate = 2;

    /// <summary>This many clean runs in a row (on different days) move a pattern to the archive.</summary>
    public const int CleanStreakToArchive = 3;

    /// <summary>Intervals until the next showing after the 1st, 2nd, 3rd clean run.</summary>
    public static readonly int[] IntervalsDays = [1, 3, 7];

    /// <summary>After a fall: tomorrow.</summary>
    public const int DaysAfterFail = 1;

    public const int BlitzMin = 5;
    public const int BlitzMax = 8;
    public const int BlitzArchiveMax = 2;

    /// <summary>Interval after cleanStreak clean runs: 1 -> 1 day, 2 -> 3 days, 3 and more -> 7 days.</summary>
    public static int IntervalAfterStreak(int cleanStreak)
    {
        if (cleanStreak <= 0) return DaysAfterFail;
        var index = Math.Min(cleanStreak, IntervalsDays.Length) - 1;
        return IntervalsDays[index];
    }
}
