namespace B1.Domain.Exam;

public static class SelfAssessment
{
    public const decimal WritingMax = 30m;
    public const decimal SpeakingMax = 40m;
    public const int WritingCriteriaCount = 5;
    public const decimal CriterionMax = 4m;

    public static decimal WritingPoints(IReadOnlyList<decimal> scores)
    {
        if (scores.Count != WritingCriteriaCount) throw new ArgumentException("Five criteria scores are required", nameof(scores));
        if (scores.Any(s => s < 0 || s > CriterionMax)) throw new ArgumentOutOfRangeException(nameof(scores), "Each score must be within 0–4");

        var sum = scores.Sum();
        return Math.Round(sum / (WritingCriteriaCount * CriterionMax) * WritingMax, 1);
    }

    /// <summary>
    /// Checklist coverage from the AI answer: how many list items it marked as done. An item counts if the AI returned
    /// it verbatim (case-insensitive); anything else it considered done is topped up to the size of the list.
    /// </summary>
    public static (int Ticked, int Total) SpeakingCoverage(IReadOnlyList<string> checklist, IEnumerable<string>? covered)
    {
        var list = (covered ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var exact = checklist.Count(item => list.Contains(item, StringComparer.OrdinalIgnoreCase));
        var other = list.Count(c => !checklist.Contains(c, StringComparer.OrdinalIgnoreCase));
        return (Math.Min(checklist.Count, exact + other), checklist.Count);
    }

    public static decimal SpeakingPoints(IReadOnlyList<(int Ticked, int Total)> tasks)
    {
        if (tasks.Count == 0 || tasks.Any(t => t.Total <= 0 || t.Ticked < 0 || t.Ticked > t.Total))
            throw new ArgumentException("Every task needs a valid checklist coverage", nameof(tasks));

        var mean = tasks.Average(t => (decimal)t.Ticked / t.Total);
        return Math.Round(mean * SpeakingMax, 1);
    }
}
