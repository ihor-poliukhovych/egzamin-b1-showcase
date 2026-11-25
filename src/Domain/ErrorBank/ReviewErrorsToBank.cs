using B1.Domain.Exam;

namespace B1.Domain.ErrorBank;

/// <summary>
/// Errors from the AI review go into the relapse bank automatically: the same error (same fragment and correction)
/// counts as one more fall of the existing relapse, a new one creates a relapse with its first fall.
/// </summary>
public static class ReviewErrorsToBank
{
    private const int MaxTitleLength = 200;

    private static readonly (string Stem, PatternCategory Category)[] CategoryByType =
    [
        ("gramat", PatternCategory.Gramatyka), ("leksyk", PatternCategory.Leksyka), ("styl", PatternCategory.Leksyka),
        ("usterk", PatternCategory.Leksyka), ("interpunk", PatternCategory.Interpunkcja), ("ortogra", PatternCategory.Ortografia),
    ];

    public static PatternCategory CategoryFor(string typ)
    {
        var t = (typ ?? string.Empty).ToLowerInvariant();
        foreach (var (stem, category) in CategoryByType)
            if (t.Contains(stem)) return category;
        return PatternCategory.Gramatyka;
    }

    /// <returns>New relapses to add to the database; existing ones are updated in place.</returns>
    public static List<ErrorPattern> Apply(IReadOnlyCollection<ErrorPattern> existing, string userId, string source, IEnumerable<ReviewError> errors,
        DateOnly date, AttemptSource attemptSource, DateTimeOffset now)
    {
        var created = new List<ErrorPattern>();
        var touched = new HashSet<ErrorPattern>();   // one review adds at most one fall per relapse
        foreach (var error in errors)
        {
            if (string.IsNullOrWhiteSpace(error.Fragment) || string.IsNullOrWhiteSpace(error.Poprawnie)) continue;

            var same = existing.Concat(created).FirstOrDefault(p => Same(p.WrongExample, error.Fragment) && Same(p.CorrectForm, error.Poprawnie));
            if (same is not null)
            {
                if (touched.Add(same)) same.Record(AttemptResult.Fail, date, attemptSource, error.Fragment);
                continue;
            }

            var title = $"{source} · {error.Typ}: {error.Fragment}";
            var pattern = new ErrorPattern(userId, title.Length > MaxTitleLength ? title[..MaxTitleLength] : title, error.Fragment.Trim(), error.Poprawnie.Trim(),
                string.IsNullOrWhiteSpace(error.Regula) ? error.Poprawnie.Trim() : error.Regula.Trim(), CategoryFor(error.Typ),
                string.IsNullOrWhiteSpace(error.Kontrast) ? null : error.Kontrast.Trim(), createdAt: now);
            pattern.Record(AttemptResult.Fail, date, attemptSource, error.Fragment);
            created.Add(pattern);
            touched.Add(pattern);
        }

        return created;
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
