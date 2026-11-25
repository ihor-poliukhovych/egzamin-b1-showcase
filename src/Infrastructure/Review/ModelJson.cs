using System.Text.Json;
using B1.Domain.Exam;

namespace B1.Infrastructure.Review;

/// <summary>Extracts the JSON result object from a model answer (plain text or the `claude -p --output-format json` envelope).</summary>
public static class ModelJson
{
    public static string ExtractResultJson(string output)
    {
        var text = output;
        try
        {
            using var doc = JsonDocument.Parse(output);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
                    throw new ReviewException($"Model returned an error: {Truncate(doc.RootElement.TryGetProperty("result", out var r) ? r.ToString() : output)}");
                if (doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String)
                    text = result.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // not a CLI envelope: look for a JSON object in the text itself
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new ReviewException("The model answer contains no JSON object with the review result");
        return text[start..(end + 1)];
    }

    internal static string Truncate(string text) => text.Length <= 400 ? text : text[..400] + "…";
}
