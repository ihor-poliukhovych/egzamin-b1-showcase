using System.Text.RegularExpressions;

namespace B1.Domain.Exam;

public class ExamTask
{
    private static readonly string[] RomanNumerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];

    protected ExamTask() { }

    public ExamTask(Guid id, Guid partId, int number, ExamTaskType type, string instruction, decimal pointsPerItem,
        string? text, string? basedOn, string? example, List<string> wordBank, List<ExamItem> items)
    {
        Id = id;
        PartId = partId;
        Number = number;
        Type = type;
        Instruction = instruction;
        PointsPerItem = pointsPerItem;
        Text = text;
        BasedOn = basedOn;
        Example = example;
        WordBank = wordBank;
        Items = items;
    }

    public Guid Id { get; set; }
    public Guid PartId { get; set; }
    public int Number { get; set; }
    public ExamTaskType Type { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public decimal PointsPerItem { get; set; }
    public string? Text { get; set; }
    public string? BasedOn { get; set; }
    public string? Example { get; set; }
    public string? Transcript { get; set; }
    /// <summary>Illustration for the task (path from the site root, for example zadania/2021-01-s5.jpg): pictures with letters from the arkusz.</summary>
    public string? Image { get; set; }
    public List<string> WordBank { get; set; } = [];
    public List<ExamItem> Items { get; set; } = [];

    public decimal MaxPoints => Items.Sum(i => PointsOf(i));

    public decimal PointsOf(ExamItem item) => item.PointsOverride ?? PointsPerItem;

    public string RomanNumber => Number is >= 1 and <= 10 ? RomanNumerals[Number - 1] : Number.ToString();

    /// <summary>
    /// The sentence of the task text with gap number n ("…w zamkniętym ___."): the error context for the error bank.
    /// Other gaps are shown as "…", solved examples as a plain word; a long sentence is trimmed around the gap.
    /// </summary>
    public string? GapSentence(int itemNumber)
    {
        if (string.IsNullOrWhiteSpace(Text)) return null;
        var marker = "{" + itemNumber + "}";
        var at = Text.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return null;

        var start = at;
        while (start > 0 && !IsSentenceEnd(Text, start - 1)) start--;
        var end = at + marker.Length;
        while (end < Text.Length && !IsSentenceEnd(Text, end)) end++;
        if (end < Text.Length) end++;

        var s = Text[start..end].Replace(marker, Gap);
        s = Regex.Replace(s, @"\{\d+\}", "…");
        s = Regex.Replace(s, @"\[\[([^\]]+)\]\]", "$1");
        s = Regex.Replace(s.Replace("[przykład]", ""), @"\s+", " ").Trim();
        if (s.Length <= MaxGapSentence) return s;

        var gap = s.IndexOf(Gap, StringComparison.Ordinal);
        var from = Math.Max(0, gap - MaxGapSentence / 2);
        var to = Math.Min(s.Length, from + MaxGapSentence);
        return (from > 0 ? "…" : "") + s[from..to].Trim() + (to < s.Length ? "…" : "");
    }

    private const string Gap = "___";
    private const int MaxGapSentence = 220;

    private static bool IsSentenceEnd(string text, int i) =>
        text[i] == '\n' || (text[i] is '.' or '!' or '?' && (i + 1 >= text.Length || char.IsWhiteSpace(text[i + 1])));

    public bool IsFreeText => Type is ExamTaskType.FillForm or ExamTaskType.AskQuestion or ExamTaskType.Transform or ExamTaskType.ShortAnswer;

    public static ExamTask Create(Guid partId, int number, ExamTaskType type, string instruction, decimal pointsPerItem,
        string? text, string? basedOn, string? example, List<string> wordBank, List<ExamItem> items)
    {
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        if (pointsPerItem <= 0) throw new ArgumentOutOfRangeException(nameof(pointsPerItem));
        return new ExamTask(Guid.NewGuid(), partId, number, type, instruction, pointsPerItem, text, basedOn, example, wordBank, items);
    }
}
