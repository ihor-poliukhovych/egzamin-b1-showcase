using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace B1.Infrastructure.Review;

public static class ReviewRegistration
{
    public const string ProviderKey = "Review:Provider";

    /// <summary>Review:Provider = none (default, no AI) | cli (local `claude -p`) | api (Claude API, key from EGZAMINB1_Review__ApiKey).</summary>
    public static IServiceCollection AddReviewers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReviewOptions>(configuration.GetSection("Review"));

        var provider = (configuration[ProviderKey] ?? "none").Trim().ToLowerInvariant();
        switch (provider)
        {
            case "none":
                services.AddSingleton<DisabledReviewer>();
                services.AddSingleton<IPisanieReviewer>(sp => sp.GetRequiredService<DisabledReviewer>());
                services.AddSingleton<IMowienieReviewer>(sp => sp.GetRequiredService<DisabledReviewer>());
                services.AddSingleton<IErrorGrouper, CategoryErrorGrouper>();
                break;
            case "cli":
                services.AddSingleton<ClaudeCliRunner>();
                services.AddSingleton<IReviewModelRunner>(sp => sp.GetRequiredService<ClaudeCliRunner>());
                services.AddSingleton<IPisanieReviewer, ClaudePisanieReviewer>();
                services.AddSingleton<IMowienieReviewer, ClaudeMowienieReviewer>();
                services.AddSingleton<IErrorGrouper, ClaudeErrorGrouper>();
                break;
            case "api":
                services.AddSingleton<IReviewModelRunner, ClaudeApiRunner>();
                services.AddSingleton<IPisanieReviewer, ClaudePisanieReviewer>();
                services.AddSingleton<IMowienieReviewer, ClaudeMowienieReviewer>();
                services.AddSingleton<IErrorGrouper, ClaudeErrorGrouper>();
                break;
            default:
                throw new InvalidOperationException($"Unknown {ProviderKey}: '{provider}'. Use 'none', 'cli' or 'api'.");
        }

        // the Mowienie task 3 partner always exists: without the model it speaks template lines
        services.AddSingleton<DialogPartner>();
        services.AddSingleton<AiUsageGate>();
        services.AddSingleton<ErrorGroupingBackoff>();
        return services;
    }
}
