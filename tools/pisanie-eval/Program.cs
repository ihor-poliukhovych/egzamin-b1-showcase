using B1.Domain.Exam;
using B1.Infrastructure.Review;
using B1.PisanieEval;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Calibration of the Pisanie AI review against works graded by human examiners (see README.md).
// Every run makes one model call per work.
//   dotnet run --project tools/pisanie-eval -- --fixtures <dir>                    all works in the folder, `claude -p`
//   dotnet run --project tools/pisanie-eval -- --fixtures <dir> --provider api     Claude API, key from EGZAMINB1_Review__ApiKey
//   dotnet run --project tools/pisanie-eval -- --fixtures <dir> --only II          one work
//   dotnet run --project tools/pisanie-eval -- --fixtures <dir> --model opus       another model (CLI alias or API model id)
//   dotnet run --project tools/pisanie-eval -- --fixtures <dir> --from <file.json> redraw the table from a saved run, no model calls
//   --out <dir> sets the output folder (default <fixtures>/out)
Console.OutputEncoding = System.Text.Encoding.UTF8;

var options = ParseArgs(args);
if (options.Fixtures is null) return Fail("Usage: --fixtures <dir> [--provider cli|api] [--model <model>] [--only <id>] [--from <file.json>] [--out <dir>]");
if (!Directory.Exists(options.Fixtures)) return Fail($"Fixtures folder not found: {options.Fixtures}");
if (options.Provider is not ("cli" or "api")) return Fail("--provider must be cli or api");

var fixtures = Calibration.Load(options.Fixtures)
    .Where(f => options.Only is null || f.Id.Equals(options.Only, StringComparison.OrdinalIgnoreCase))
    .ToList();
if (fixtures.Count == 0) return Fail("No fixtures found (check --fixtures and --only)");

List<FixtureOutcome> outcomes;
if (options.From is not null)
{
    var saved = Calibration.Deserialize<Dictionary<string, PisanieReviewResult>>(File.ReadAllText(options.From));
    outcomes = fixtures.Where(f => saved.ContainsKey(f.Id)).Select(f => Calibration.Compare(f, saved[f.Id])).ToList();
}
else
{
    var runner = CreateRunner(options.Provider, options.Model);
    if (runner is null) return Fail("Set the environment variable EGZAMINB1_Review__ApiKey for --provider api");

    var results = new Dictionary<string, PisanieReviewResult>();
    var raw = new Dictionary<string, System.Text.Json.JsonElement>();
    foreach (var fixture in fixtures)
    {
        Console.Error.WriteLine($"→ {fixture.Id} …");
        try
        {
            // same as ClaudePisanieReviewer.ReviewAsync, but the raw answer (liczby, pasma) is kept for analysis
            var json = ModelJson.ExtractResultJson(await runner.RunAsync(ClaudePisanieReviewer.BuildPrompt(fixture.ToRequest()), CancellationToken.None));
            raw[fixture.Id] = System.Text.Json.JsonDocument.Parse(json).RootElement.Clone();
            results[fixture.Id] = ClaudePisanieReviewer.Parse(json);
        }
        catch (ReviewException e)
        {
            Console.Error.WriteLine($"  error: {e.Message}");
        }
    }

    var outDir = options.Out ?? Path.Combine(options.Fixtures, "out");
    Directory.CreateDirectory(outDir);
    var outFile = Path.Combine(outDir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{options.Provider}-{SafeName(options.Model)}.json");
    File.WriteAllText(outFile, Calibration.Serialize(results));
    File.WriteAllText(Path.ChangeExtension(outFile, ".raw.json"), Calibration.Serialize(raw));
    Console.Error.WriteLine($"Results: {outFile}");

    outcomes = fixtures.Where(f => results.ContainsKey(f.Id)).Select(f => Calibration.Compare(f, results[f.Id])).ToList();
}

Console.WriteLine(Calibration.ToMarkdown(outcomes));
return outcomes.Count == fixtures.Count && outcomes.All(o => o.Pass) ? 0 : 1;

static IReviewModelRunner? CreateRunner(string provider, string? model)
{
    if (provider == "cli")
        return new ClaudeCliRunner(Options.Create(new ReviewOptions { Provider = "cli", Model = model ?? "sonnet", TimeoutSeconds = 300 }),
            new ConsoleLogger<ClaudeCliRunner>());

    var apiKey = Environment.GetEnvironmentVariable("EGZAMINB1_Review__ApiKey");
    if (string.IsNullOrWhiteSpace(apiKey)) return null;
    var reviewOptions = new ReviewOptions { Provider = "api", ApiKey = apiKey, TimeoutSeconds = 300 };
    if (!string.IsNullOrWhiteSpace(model)) reviewOptions.ApiModel = model;
    return new ClaudeApiRunner(Options.Create(reviewOptions), new ConsoleLogger<ClaudeApiRunner>());
}

static string SafeName(string? value) => string.IsNullOrWhiteSpace(value) ? "default" : string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '_'));

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

static EvalOptions ParseArgs(string[] args)
{
    var result = new EvalOptions();
    for (var i = 0; i < args.Length - 1; i++)
    {
        switch (args[i])
        {
            case "--fixtures": result.Fixtures = args[++i]; break;
            case "--provider": result.Provider = args[++i].Trim().ToLowerInvariant(); break;
            case "--only": result.Only = args[++i]; break;
            case "--model": result.Model = args[++i]; break;
            case "--from": result.From = args[++i]; break;
            case "--out": result.Out = args[++i]; break;
        }
    }

    return result;
}

internal sealed class EvalOptions
{
    public string? Fixtures { get; set; }
    public string Provider { get; set; } = "cli";
    public string? Only { get; set; }
    public string? Model { get; set; }
    public string? From { get; set; }
    public string? Out { get; set; }
}

internal sealed class ConsoleLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Console.Error.WriteLine($"  [{logLevel}] {formatter(state, exception)}");
}
