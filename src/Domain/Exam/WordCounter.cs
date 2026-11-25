using System.Text.RegularExpressions;

namespace B1.Domain.Exam;

public enum WordCountStatus
{
    Under = 0,
    Ok = 1,
    Over = 2,
}

public static class WordCounter
{
    private const string Months = "stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|października|listopada|grudnia";

    private static readonly Regex NumericDate = new(@"(?<!\d)\d{1,2}[./-]\d{1,2}[./-]\d{2,4}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex TextDate = new($@"(?<!\d)\d{{1,2}}\s+({Months})(\s+\d{{4}})?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Phone = new(@"(?<!\d)(\+?\d{1,3}[ -]?)?\d{3}[ -]?\d{3}[ -]?\d{3}(?!\d)", RegexOptions.Compiled);

    public static int Count(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        var count = 0;
        var rest = text;
        foreach (var pattern in new[] { NumericDate, TextDate })
        {
            count += 2 * pattern.Matches(rest).Count;
            rest = pattern.Replace(rest, " ");
        }

        count += Phone.Matches(rest).Count;
        rest = Phone.Replace(rest, " ");

        foreach (var token in rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Contains('@'))
            {
                count++;
                continue;
            }

            count += token
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Count(part => part.Any(char.IsLetterOrDigit));
        }

        return count;
    }

    public static (int Min, int Max) Range(int target) => target <= 50
        ? ((int)Math.Ceiling(target * 0.9), (int)Math.Floor(target * 1.1))
        : (target - 20, target + 20);

    public static WordCountStatus Status(int count, int target)
    {
        var (min, max) = Range(target);
        return count < min ? WordCountStatus.Under : count > max ? WordCountStatus.Over : WordCountStatus.Ok;
    }
}
