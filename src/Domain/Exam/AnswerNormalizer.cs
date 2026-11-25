using System.Text;

namespace B1.Domain.Exam;

public static class AnswerNormalizer
{
    private const string LeadingDashes = "-–—";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value.Trim().TrimStart(LeadingDashes.ToCharArray()))
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0) builder.Append(' ');
                pendingSpace = false;
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch == '-')
                pendingSpace = true;
        }

        return builder.ToString();
    }

    public static bool Matches(string? value, IEnumerable<string> answerKey)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0) return false;
        return answerKey.Any(key => Normalize(key) == normalized);
    }
}
