using Anthropic;
using Anthropic.Models.Messages;
using B1.Domain.Exam;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace B1.Infrastructure.Review;

/// <summary>Source of the model answer: local `claude -p` or the Claude API. Prompts and parsing are shared.</summary>
public interface IReviewModelRunner
{
    /// <summary>Sends the prompt and returns the answer text; <see cref="ModelJson.ExtractResultJson"/> extracts the JSON from it.</summary>
    Task<string> RunAsync(string prompt, CancellationToken ct);
}

/// <summary>
/// Review through the Claude API (Review:Provider = api). The key is Review:ApiKey, supplied as the environment variable
/// EGZAMINB1_Review__ApiKey. No tools: the student's text is only in the user message and the model answers with one JSON object.
/// </summary>
public class ClaudeApiRunner : IReviewModelRunner
{
    private const string SystemPrompt = "You are an examiner of Polish as a foreign language. Reply with one JSON object only.";
    private const int MaxTokens = 4000;

    private readonly ReviewOptions _options;
    private readonly ILogger<ClaudeApiRunner> _logger;
    private readonly Lazy<AnthropicClient> _client;

    public ClaudeApiRunner(IOptions<ReviewOptions> options, ILogger<ClaudeApiRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<AnthropicClient>(() => new AnthropicClient { ApiKey = _options.ApiKey, MaxRetries = _options.ApiMaxRetries });
    }

    public async Task<string> RunAsync(string prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) throw new ReviewException("Claude API key is not configured (Review:ApiKey); AI review is unavailable");
        _logger.LogInformation("ClaudeApiRunner.RunAsync STARTED. model={Model}, promptLength={Length}", _options.ApiModel, prompt.Length);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        Message response;
        try
        {
            response = await _client.Value.Messages.Create(new MessageCreateParams
            {
                Model = _options.ApiModel,
                MaxTokens = MaxTokens,
                System = SystemPrompt,
                Messages = [new UserMessageParam { Content = prompt }],
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ReviewException($"Claude API did not answer within {_options.TimeoutSeconds} s; the review was aborted");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "ClaudeApiRunner.RunAsync FAILED");
            throw new ReviewException($"Claude API error: {e.Message}");
        }

        if (response.StopReason?.ToString() == "refusal")
            throw new ReviewException("The model refused to review this text");

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        _logger.LogInformation("ClaudeApiRunner.RunAsync FINISHED. stop={Stop}, inputTokens={In}, outputTokens={Out}",
            response.StopReason?.ToString(), response.Usage.InputTokens, response.Usage.OutputTokens);
        return text;
    }
}
