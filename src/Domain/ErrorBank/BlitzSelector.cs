namespace B1.Domain.ErrorBank;

/// <summary>
/// Daily blitz selection: 5-8 items. First those due by nextDueAt (the most frequent among them),
/// then, if the minimum is not reached, the most frequent active ones, plus 1-2 from the archive as a check.
/// Fresh errors (a single fall, status Watch, for example from the AI review of Pisanie) are added at the end
/// if there is room left up to the maximum: a new error is practised right away but does not displace relapses.
/// </summary>
public static class BlitzSelector
{
    public static IReadOnlyList<ErrorPattern> Select(IEnumerable<ErrorPattern> patterns, DateOnly today,
        int min = ReviewPolicy.BlitzMin, int max = ReviewPolicy.BlitzMax, int archiveMax = ReviewPolicy.BlitzArchiveMax)
    {
        if (min < 0 || max < min) throw new ArgumentOutOfRangeException(nameof(max), "max must be >= min >= 0");

        var all = patterns.ToList();

        var archived = all.Where(p => p.Status == PatternStatus.Archived)
            .OrderBy(p => p.NextDueAt ?? DateOnly.MinValue)
            .ThenByDescending(p => p.FallsCount)
            .Take(archiveMax)
            .ToList();

        var active = all.Where(p => p.Status == PatternStatus.Active).ToList();

        var due = active.Where(p => p.NextDueAt is null || p.NextDueAt <= today)
            .OrderByDescending(p => p.FallsCount)
            .ThenBy(p => p.NextDueAt ?? DateOnly.MinValue)
            .ToList();

        var activeBudget = Math.Max(0, max - archived.Count);
        var picked = due.Take(activeBudget).ToList();

        if (picked.Count + archived.Count < min)
        {
            var fillers = active.Except(picked)
                .OrderByDescending(p => p.FallsCount)
                .ThenBy(p => p.NextDueAt ?? DateOnly.MinValue)
                .Take(min - picked.Count - archived.Count);
            picked.AddRange(fillers);
        }

        var fresh = all.Where(p => p.Status == PatternStatus.Watch && p.FallsCount > 0)
            .OrderByDescending(p => p.LastFailDate ?? DateOnly.MinValue)
            .ThenByDescending(p => p.FallsCount)
            .Take(Math.Max(0, max - picked.Count - archived.Count));
        picked.AddRange(fresh);

        picked.AddRange(archived);
        return picked;
    }
}
