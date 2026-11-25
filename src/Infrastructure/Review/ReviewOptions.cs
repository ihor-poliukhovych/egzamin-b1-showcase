namespace B1.Infrastructure.Review;

/// <summary>Configuration section "Review".</summary>
public class ReviewOptions
{
    /// <summary>none (no AI, self-assessment only) | cli (local `claude -p`) | api (Claude API).</summary>
    public string Provider { get; set; } = "none";

    /// <summary>Command used for Provider = cli.</summary>
    public string ClaudeCommand { get; set; } = "claude";

    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>Model alias for `claude -p`. Empty means the CLI default.</summary>
    public string Model { get; set; } = "sonnet";

    /// <summary>Claude API key (Review:ApiKey). Supply it only through the environment (EGZAMINB1_Review__ApiKey), never in files.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Claude API model for Provider = api.</summary>
    public string ApiModel { get; set; } = "claude-opus-5-5";

    /// <summary>How many times the API client retries a failed request (rate limit, overload, network).</summary>
    public int ApiMaxRetries { get; set; } = 2;

    /// <summary>Pisanie/Mowienie reviews per user per UTC day (also the limit for error-grouping calls).</summary>
    public int DailyLimit { get; set; } = 50;

    /// <summary>AI-adapted dialog lines per user per UTC day; after that the partner uses template lines.</summary>
    public int DailyDialogTurns { get; set; } = 200;
}
