using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using B1.Domain.Exam;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace B1.Infrastructure.Review;

/// <summary>Review through the local `claude -p` command (Review:Provider = cli), meant for development.</summary>
public class ClaudeCliRunner : IReviewModelRunner
{
    private readonly ReviewOptions _options;
    private readonly ILogger<ClaudeCliRunner> _logger;

    public ClaudeCliRunner(IOptions<ReviewOptions> options, ILogger<ClaudeCliRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> RunAsync(string prompt, CancellationToken ct)
    {
        _logger.LogInformation("ClaudeCliRunner.RunAsync STARTED. promptLength={Length}", prompt.Length);

        using var process = StartProcess();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            // start reading before writing: a large prompt and a full output pipe would otherwise block each other
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

            await process.StandardInput.WriteAsync(prompt.AsMemory(), timeout.Token);
            process.StandardInput.Close();

            await process.WaitForExitAsync(timeout.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
                throw new ReviewException($"claude CLI exited with code {process.ExitCode}: {ModelJson.Truncate(stderr.Length > 0 ? stderr : stdout)}");

            _logger.LogInformation("ClaudeCliRunner.RunAsync FINISHED. outputLength={Length}, usage={Usage}", stdout.Length, DescribeUsage(stdout));
            return stdout;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ReviewException($"claude CLI did not answer within {_options.TimeoutSeconds} s; the review was aborted");
        }
        finally
        {
            // timeout, client cancellation or any failure: never leave the CLI and its child processes running
            TryKill(process);
        }
    }

    /// <summary>Usage from the --output-format json envelope: model, tokens and cost of one review.</summary>
    public static string DescribeUsage(string stdout)
    {
        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            string Read(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ToString() : "?";
            var usage = root.TryGetProperty("usage", out var u) ? u : default;
            var model = root.TryGetProperty("modelUsage", out var mu) && mu.ValueKind == JsonValueKind.Object
                ? string.Join(",", mu.EnumerateObject().Select(p => p.Name))
                : "?";
            return usage.ValueKind == JsonValueKind.Object
                ? $"model={model} in={Read(usage, "input_tokens")} cacheRead={Read(usage, "cache_read_input_tokens")} cacheWrite={Read(usage, "cache_creation_input_tokens")} out={Read(usage, "output_tokens")} cost=${Read(root, "total_cost_usd")} ms={Read(root, "duration_ms")}"
                : "n/a";
        }
        catch (JsonException)
        {
            return "n/a";
        }
    }

    /// <summary>
    /// The prompt goes through stdin and the arguments are fixed. The prompt contains student text, so the model gets no tools
    /// (--tools "") and no MCP servers from user settings (--strict-mcp-config): an injection in the text cannot read files or run commands.
    /// </summary>
    public static IReadOnlyList<string> CliArguments { get; } = ["-p", "--output-format", "json", "--tools", "", "--strict-mcp-config"];

    /// <summary>
    /// On top of the safety arguments: no session persistence, a short system prompt of our own
    /// (no quotes or cmd special characters, since it may also go through cmd.exe /c) and an optional model.
    /// </summary>
    private string[] BuildArgs()
    {
        List<string> args =
        [
            .. CliArguments,
            "--no-session-persistence",
            "--system-prompt", "You are an examiner of Polish as a foreign language. Reply with one JSON object only.",
        ];
        if (!string.IsNullOrWhiteSpace(_options.Model)) args.AddRange(["--model", _options.Model]);
        return [.. args];
    }

    private Process StartProcess()
    {
        var args = BuildArgs();
        try
        {
            return Launch(_options.ClaudeCommand, args);
        }
        catch (Win32Exception) when (OperatingSystem.IsWindows())
        {
            try
            {
                return Launch("cmd.exe", ["/c", _options.ClaudeCommand, .. args]);
            }
            catch (Win32Exception)
            {
                throw NotFound();
            }
        }
        catch (Win32Exception)
        {
            throw NotFound();
        }
    }

    private static ReviewException NotFound() =>
        new("claude CLI was not found in PATH. Install it and sign in, set Review:ClaudeCommand, or use Review:Provider=api or none.");

    private static Process Launch(string fileName, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(fileName)
        {
            // an empty working directory: the CLI does not pick up project instructions or settings from the Api folder
            WorkingDirectory = WorkingDirectory(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return Process.Start(info) ?? throw new Win32Exception("Process did not start");
    }

    public static string WorkingDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "b1-review");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // the process has already exited
        }
    }
}
