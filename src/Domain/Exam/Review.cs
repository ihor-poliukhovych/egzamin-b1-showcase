namespace B1.Domain.Exam;

/// <summary>
/// An error found by the review becomes a future recurring error. Three steps: Fragment (what was written) → Poprawnie (the correct form) →
/// Regula (the rule, why it is wrong, a self-check technique). Typ follows the examiners' legend (<see cref="ReviewErrorTypes"/>).
/// </summary>
public record ReviewError(string Fragment, string Poprawnie, string Typ, string Regula, string? Kontrast = null, string? Zadanie = null);

/// <summary>One task of a Pisanie zestaw (a is the short form, b the longer one).</summary>
public record PisanieTaskInput(string Zadanie, string TaskPrompt, string Form, int TargetWords, string Text);

/// <summary>The whole work: examiners give one score per each of the 5 criteria for both tasks together.</summary>
public record PisanieReviewRequest(IReadOnlyList<PisanieTaskInput> Tasks, string Language = ReviewLanguage.Default);

public record PisanieReviewResult(Dictionary<string, decimal> Scores, List<ReviewError> Errors, string Komentarz, string? Werdykt = null);

public record MowienieTaskInput(int Number, string Kind, string Prompt, IReadOnlyList<string> Checklist, string Notes, string Spoken);

public record MowienieReviewRequest(IReadOnlyList<MowienieTaskInput> Tasks, string Language = ReviewLanguage.Default);

/// <summary>
/// Language of the model's explanations = UI language at review time (uk, ru, pl, en). A finished review is stored as is:
/// changing the language later only affects new reviews.
/// </summary>
public static class ReviewLanguage
{
    public const string Default = "uk";
    private static readonly string[] Supported = ["uk", "ru", "pl", "en"];

    public static string Normalize(string? preferred, string? fallback = null)
    {
        foreach (var candidate in new[] { preferred, fallback })
        {
            var code = candidate?.Trim().ToLowerInvariant();
            if (code is not null && Supported.Contains(code)) return code;
        }
        return Default;
    }

    /// <summary>How to name the language inside the Polish prompt, e.g. "po ukraińsku".</summary>
    public static string InPolish(string language) => Normalize(language) switch
    {
        "pl" => "po polsku",
        "ru" => "po rosyjsku",
        "en" => "po angielsku",
        _ => "po ukraińsku",
    };
}

public record MowienieTaskReview(int Task, List<string> Covered, List<string> Missing, string Komentarz);

public record MowienieReviewResult(List<MowienieTaskReview> Tasks, string Komentarz);

public interface IPisanieReviewer
{
    Task<PisanieReviewResult> ReviewAsync(PisanieReviewRequest request, CancellationToken ct = default);
}

public interface IMowienieReviewer
{
    Task<MowienieReviewResult> ReviewAsync(MowienieReviewRequest request, CancellationToken ct = default);
}

/// <summary>
/// AI review failure. <see cref="Code"/> is a stable code for the client (returned as ProblemDetails extension "code");
/// the message is for logs only and is never sent to the client.
/// </summary>
public class ReviewException(string message, string code = ReviewException.Failed) : Exception(message)
{
    public const string NoText = "review-no-text";
    public const string Limit = "review-limit";
    public const string Failed = "review-failed";

    public string Code { get; } = code;
}

/// <summary>Error type legend used in the examiners' published graded works.</summary>
public static class ReviewErrorTypes
{
    public const string Gramatyczny = "błąd gramatyczny";
    public const string Leksykalny = "błąd leksykalny";
    public const string Stylistyczny = "błąd stylistyczny";
    public const string Interpunkcyjny = "błąd interpunkcyjny";
    public const string Ortograficzny = "błąd ortograficzny";
    public const string Usterka = "usterka stylistyczna";

    public static readonly IReadOnlyList<string> All = [Gramatyczny, Leksykalny, Stylistyczny, Interpunkcyjny, Ortograficzny, Usterka];

    /// <summary>Maps a model answer ("gramatyczny", "Błąd leksykalny", "usterka") to a legend name; unknown values give null.</summary>
    public static string? Normalize(string? typ)
    {
        if (string.IsNullOrWhiteSpace(typ)) return null;
        var t = typ.Trim().ToLowerInvariant();
        if (t.Contains("usterk")) return Usterka;
        if (t.Contains("gramat")) return Gramatyczny;
        if (t.Contains("leksyk")) return Leksykalny;
        if (t.Contains("styl")) return Stylistyczny;
        if (t.Contains("interpunk")) return Interpunkcyjny;
        if (t.Contains("ortogra")) return Ortograficzny;
        return null;
    }
}

/// <summary>
/// Verdict from the sum of the 5 criteria (0–20). Thresholds were calibrated on a few published graded works
/// (about 55 % "nie spełnia", 69 % "poprawna", 86 % "dość dobra", 94 % "bardzo dobra"). This is not an official scale.
/// </summary>
public static class PisanieVerdict
{
    public const string BardzoDobra = "bardzo dobra";
    public const string DoscDobra = "dość dobra";
    public const string Poprawna = "poprawna";
    public const string NieSpelnia = "nie spełnia wymagań B1";

    public static string FromScores(IReadOnlyDictionary<string, decimal> scores)
    {
        var share = scores.Values.Sum() / (SelfAssessment.WritingCriteriaCount * SelfAssessment.CriterionMax);
        return share switch
        {
            >= 0.9m => BardzoDobra,
            >= 0.8m => DoscDobra,
            >= 0.6m => Poprawna,
            _ => NieSpelnia,
        };
    }
}
