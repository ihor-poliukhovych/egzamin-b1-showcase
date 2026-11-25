using System.Globalization;
using System.Text;
using System.Text.Json;
using B1.Domain.Exam;

namespace B1.PisanieEval;

public record FixtureTask(string Zadanie, string Polecenie, string Forma, int Slowa, string Tekst);

/// <summary>A graded work with the scores of two examiners (one JSON file per work in the fixtures folder; see README.md).</summary>
public record Fixture(
    string Id,
    string Zestaw,
    List<FixtureTask> Zadania,
    Dictionary<string, decimal> EgzaminatorA,
    Dictionary<string, decimal> EgzaminatorB,
    string Werdykt,
    string KomentarzKomisji)
{
    /// <summary>Target for the model: the mean of the two examiners on a single examiner's scale (0–4).</summary>
    public Dictionary<string, decimal> Expected => Calibration.Criteria.ToDictionary(k => k, k => (EgzaminatorA[k] + EgzaminatorB[k]) / 2);

    public PisanieReviewRequest ToRequest() =>
        new(Zadania.Select(z => new PisanieTaskInput(z.Zadanie, z.Polecenie, z.Forma, z.Slowa, z.Tekst)).ToList());
}

public record CriterionDiff(string Key, decimal Expected, decimal Actual)
{
    public decimal Delta => Actual - Expected;
    public bool Ok => Math.Abs(Delta) <= Calibration.Tolerance;
}

public record FixtureOutcome(Fixture Fixture, PisanieReviewResult Result, List<CriterionDiff> Diffs)
{
    public bool VerdictOk => string.Equals(Result.Werdykt, Fixture.Werdykt, StringComparison.OrdinalIgnoreCase);
    public bool Pass => VerdictOk && Diffs.All(d => d.Ok);
}

public static class Calibration
{
    public const decimal Tolerance = 0.5m;
    public static readonly string[] Criteria = ["wykonanie", "gramatyka", "slownictwo", "styl", "ortografia"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static List<Fixture> Load(string dir) =>
        Directory.GetFiles(dir, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(f => JsonSerializer.Deserialize<Fixture>(File.ReadAllText(f), JsonOptions)
                         ?? throw new InvalidDataException($"Empty fixture {f}"))
            .ToList();

    public static FixtureOutcome Compare(Fixture fixture, PisanieReviewResult result)
    {
        var expected = fixture.Expected;
        var diffs = Criteria.Select(k => new CriterionDiff(k, expected[k], result.Scores.GetValueOrDefault(k))).ToList();
        return new FixtureOutcome(fixture, result, diffs);
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidDataException("empty json");

    /// <summary>Markdown table: per work "model (Δ)" for each criterion, and the verdicts.</summary>
    public static string ToMarkdown(IReadOnlyList<FixtureOutcome> outcomes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Praca | Wyk. | Gram. | Słow. | Styl | Ort. | Werdykt modelu | Werdykt Komisji | OK |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var o in outcomes)
        {
            var cells = o.Diffs.Select(d => $"{F(d.Actual)} ({(d.Delta >= 0 ? "+" : "")}{F(d.Delta)}){(d.Ok ? "" : " ⚠")}");
            sb.AppendLine($"| {o.Fixture.Id} | {string.Join(" | ", cells)} | {o.Result.Werdykt} | {o.Fixture.Werdykt} | {(o.Pass ? "✅" : "❌")} |");
        }

        var all = outcomes.SelectMany(o => o.Diffs).ToList();
        if (all.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Criteria within ±{F(Tolerance)}: {all.Count(d => d.Ok)}/{all.Count}; MAE {F(all.Average(d => Math.Abs(d.Delta)))}; " +
                          $"mean bias {F(all.Average(d => d.Delta))}; verdict matched: {outcomes.Count(o => o.VerdictOk)}/{outcomes.Count}; " +
                          $"works fully passed: {outcomes.Count(o => o.Pass)}/{outcomes.Count}.");
        }

        return sb.ToString();
    }

    public static string F(decimal value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
