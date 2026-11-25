namespace B1.Domain.Exam;

/// <summary>One step of a dialog scenario: what the conversation partner should do (<see cref="Intent"/>) and a model line (<see cref="Line"/>).</summary>
public record DialogBeat(string Intent, string Line);

/// <summary>
/// Role-play template for Mowienie, task 3 ("sytuacja"): who the examiner plays, who starts and the plan of lines.
/// The model adapts every line to what the candidate said; without the model the template line is used as is.
/// </summary>
public record DialogTemplate(string Role, bool ExaminerOpens, IReadOnlyList<DialogBeat> Beats);

/// <summary>A line of the dialog: <see cref="Who"/> is <c>examiner</c> or <c>candidate</c>.</summary>
public record DialogLine(string Who, string Text);

public static class Dialogs
{
    public const string Examiner = "examiner";
    public const string Candidate = "candidate";
    public const int MaxLineChars = 600;
    public const int MaxHistory = 20;

    // Generic everyday situations, recognised by keywords in the task prompt. The first template whose keyword occurs wins.
    private static readonly (string[] Keywords, DialogTemplate Template)[] Known =
    [
        (["lekarz", "przychodni", "rejestracj"], new DialogTemplate("recepcjonistka w przychodni", true,
        [
            new("odbierz telefon i zapytaj, w czym możesz pomóc", "Przychodnia, dzień dobry. W czym mogę pomóc?"),
            new("zapytaj, co dolega pacjentowi", "Rozumiem. Co Panu / Pani dolega?"),
            new("zaproponuj termin wizyty", "Lekarz ma wolny termin w czwartek o dziesiątej. Pasuje?"),
            new("poproś o imię, nazwisko i numer telefonu", "Dobrze. Proszę podać imię, nazwisko i numer telefonu."),
            new("potwierdź wizytę i zakończ rozmowę", "Zapisałam. Do widzenia!"),
        ])),
        (["hotel", "pokój", "nocleg"], new DialogTemplate("recepcjonista w hotelu", true,
        [
            new("przywitaj gościa i zapytaj, w czym pomóc", "Hotel „Pod Lipą”, dzień dobry. Słucham?"),
            new("zapytaj o termin i liczbę osób", "Na kiedy i dla ilu osób?"),
            new("powiedz, jaki pokój jest wolny, i podaj cenę", "Mamy wolny pokój dwuosobowy, kosztuje 300 złotych za noc."),
            new("zapytaj o śniadanie lub parking", "Czy potrzebuje Pan / Pani śniadania albo parkingu?"),
            new("potwierdź rezerwację i zakończ rozmowę", "Rezerwacja przyjęta. Do zobaczenia!"),
        ])),
        (["kino", "kina", "film", "koncert"], new DialogTemplate("kolega (mów na „ty”)", false,
        [
            new("zareaguj na propozycję bez entuzjazmu", "Hm, nie wiem. Jaki to film?"),
            new("zapytaj o dzień i godzinę", "A kiedy? W tygodniu mam dużo pracy."),
            new("zapytaj o cenę biletów", "Ile kosztują bilety?"),
            new("zaproponuj zmianę planu", "Może pójdziemy razem na pizzę po filmie?"),
            new("zgódź się i zakończ rozmowę", "Dobra, umowa. Do zobaczenia!"),
        ])),
        (["sklep", "kupi", "zwróci", "reklamacj"], new DialogTemplate("sprzedawca w sklepie", true,
        [
            new("przywitaj klienta i zapytaj, w czym pomóc", "Dzień dobry, w czym mogę pomóc?"),
            new("dopytaj o szczegóły (rozmiar, kolor, problem)", "Jaki rozmiar albo kolor Pana / Panią interesuje?"),
            new("zaproponuj konkretną rzecz lub rozwiązanie", "Mam coś podobnego, ale trochę droższego. Pokazać?"),
            new("podaj cenę i zapytaj o formę płatności", "To kosztuje 120 złotych. Karta czy gotówka?"),
            new("podziękuj i zakończ rozmowę", "Dziękuję, do widzenia!"),
        ])),
    ];

    // For a situation without its own scenario: a general plan; the model takes the role from the prompt
    private static readonly DialogTemplate Generic = new("rozmówca z polecenia (dobierz rolę do sytuacji)", false,
    [
        new("zareaguj na początek rozmowy i dopytaj o szczegóły", "Rozumiem. Może Pani / Pan powiedzieć więcej?"),
        new("zapytaj o powód lub ważny szczegół", "Dlaczego to ważne?"),
        new("zgłoś wątpliwość lub trudność", "Hm, nie wiem. Jest inne rozwiązanie?"),
        new("zgódź się i zakończ rozmowę", "Dobrze, zgoda. Dziękuję!"),
    ]);

    public static DialogTemplate For(string? prompt)
    {
        var text = prompt ?? string.Empty;
        foreach (var (keywords, template) in Known)
            if (keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) return template;
        return Generic;
    }

    /// <summary>Current scenario step: the number of examiner lines said so far.</summary>
    public static int NextBeat(IReadOnlyList<DialogLine> history) => history.Count(h => h.Who == Examiner);

    /// <summary>History from the request: only the two roles, non-empty lines, limited length and number of lines.</summary>
    public static List<DialogLine> Sanitize(IEnumerable<DialogLine>? history) =>
        (history ?? [])
            .Where(h => (h.Who == Examiner || h.Who == Candidate) && !string.IsNullOrWhiteSpace(h.Text))
            .Select(h => h with { Text = Clip(h.Text.Trim(), MaxLineChars) })
            .TakeLast(MaxHistory)
            .ToList();

    /// <summary>The dialog as text for the Mowienie review: "Egzaminator: …" / "Zdający: …".</summary>
    public static string Transcript(IEnumerable<DialogLine> history) =>
        string.Join('\n', history.Select(h => $"{(h.Who == Examiner ? "Egzaminator" : "Zdający")}: {h.Text}"));

    public static string Clip(string text, int max) => text.Length > max ? text[..max] : text;
}
