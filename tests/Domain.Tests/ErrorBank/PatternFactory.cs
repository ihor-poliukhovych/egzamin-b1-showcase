using B1.Domain.ErrorBank;

namespace B1.Domain.Tests.ErrorBank;

internal static class PatternFactory
{
    public static readonly DateOnly Day1 = new(2026, 10, 1);
    public static DateOnly Day(int n) => Day1.AddDays(n - 1);

    public static ErrorPattern New(string title = "rozmawiać z + narzędnik", string code = "R1") =>
        new("student-1", title, "rozmawiam z moja koleżanka", "rozmawiam z moją koleżanką",
            "After rozmawiać z comes narzędnik.", PatternCategory.Rekcja, "The Russian construction takes the instrumental too, but the endings differ.", code);

    /// <summary>A pattern with a given number of falls (each on a separate day, starting from Day1).</summary>
    public static ErrorPattern WithFalls(int falls, string code = "R1")
    {
        var p = New(code: code, title: $"pattern {code}");
        for (var i = 0; i < falls; i++)
            p.Record(AttemptResult.Fail, Day(i + 1));
        return p;
    }

    /// <summary>An active pattern (2 falls on Day1-Day2), followed by N clean runs on different days starting from Day3.</summary>
    public static ErrorPattern ActiveWithCleanStreak(int streak, string code = "R1")
    {
        var p = WithFalls(2, code);
        for (var i = 0; i < streak; i++)
            p.Record(AttemptResult.Pass, Day(3 + i));
        return p;
    }
}
