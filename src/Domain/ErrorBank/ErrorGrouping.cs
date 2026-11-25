namespace B1.Domain.ErrorBank;

/// <summary>A relapse sent to the AI for grouping: a short number instead of a Guid saves tokens.</summary>
public record GroupingItem(int Number, string WrongExample, string CorrectForm, string Rule);

/// <summary>AI answer: the relapse with number <see cref="Number"/> belongs to type <see cref="Group"/>; <see cref="Rule"/> is a short rule if the error had only the task context.</summary>
public record GroupAssignment(int Number, string Group, string? Rule = null);

/// <summary>Groups errors by type (one grammar topic is one group) so the blitz runs in blocks.</summary>
public interface IErrorGrouper
{
    Task<IReadOnlyList<GroupAssignment>> GroupAsync(IReadOnlyList<GroupingItem> items, IReadOnlyList<string> existingGroups, string language, CancellationToken ct = default);

    /// <summary>Whether a call costs a model request (and so counts towards the daily AI limit).</summary>
    bool UsesModel => true;
}

/// <summary>
/// Grouping rules: only ungrouped relapses go to the AI (no more than <see cref="BatchSize"/> per call),
/// a group name that matches an existing one case-insensitively is normalised to it, and whatever the AI skipped
/// falls into a group by category, so the AI is not asked about the same relapses every time the blitz is opened.
/// </summary>
public static class ErrorGrouping
{
    public const int BatchSize = 30;
    public const int MaxGroupNameLength = 80;

    /// <summary>How the "rule" of a Gramatyka error without an explanation in the key begins: it is only the task context, the AI will add the rule.</summary>
    public const string ContextRulePrefix = "Zadanie ";

    public static List<ErrorPattern> Pending(IEnumerable<ErrorPattern> patterns) =>
        patterns.Where(p => p.GroupName is null && p.Status != PatternStatus.Archived)
            .OrderByDescending(p => p.LastFailDate).ThenByDescending(p => p.FallsCount)
            .Take(BatchSize)
            .ToList();

    public static List<string> ExistingGroups(IEnumerable<ErrorPattern> patterns) =>
        patterns.Where(p => p.GroupName is not null).Select(p => p.GroupName!).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

    public static List<GroupingItem> ToItems(IReadOnlyList<ErrorPattern> pending) =>
        pending.Select((p, i) => new GroupingItem(i + 1, p.WrongExample, p.CorrectForm, p.Rule)).ToList();

    public static void Apply(IReadOnlyList<ErrorPattern> pending, IReadOnlyList<string> existingGroups, IEnumerable<GroupAssignment> assignments)
    {
        var known = existingGroups.ToDictionary(g => g, g => g, StringComparer.OrdinalIgnoreCase);
        foreach (var a in assignments)
        {
            if (a.Number < 1 || a.Number > pending.Count || string.IsNullOrWhiteSpace(a.Group)) continue;
            var pattern = pending[a.Number - 1];
            if (pattern.GroupName is not null) continue;
            var name = a.Group.Trim();
            if (known.TryGetValue(name, out var canonical)) name = canonical;
            else known[name] = name;
            pattern.AssignGroup(name);
            if (!string.IsNullOrWhiteSpace(a.Rule)) pattern.ExplainRule(a.Rule);
        }

        foreach (var pattern in pending.Where(p => p.GroupName is null))
            pattern.AssignGroup(pattern.Category.ToString());
    }
}
