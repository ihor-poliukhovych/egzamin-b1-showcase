using B1.Domain.Exam;

namespace B1.Domain.Tests.Exam;

internal static class ArkuszFactory
{
    public static readonly DateTimeOffset StartedAt = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset SubmittedAt = StartedAt.AddMinutes(40);

    public static ExamTask ChooseTask(Guid arkuszId, int number = 1, decimal pointsPerItem = 0.5m, int items = 2)
    {
        var task = ExamTask.Create(arkuszId, number, ExamTaskType.ChooseInText, "Proszę podkreślić poprawne formy.", pointsPerItem,
            "Rozmawiałam z {1} koleżanką o {2} książce.", null, null, [], []);
        task.Items = Enumerable.Range(1, items)
            .Select(n => ExamItem.Create(task.Id, n, null, null, ["tę", "tą", "ta"], ["tą"], null))
            .ToList();
        return task;
    }

    public static ExamTask WordBankTask(Guid arkuszId, int number = 2)
    {
        var task = ExamTask.Create(arkuszId, number, ExamTaskType.WordBank, "Proszę uzupełnić zdania wyrazami z ramki.", 0.5m,
            "Jest głośno, {1} coś się dzieje.", null, null, ["ponieważ", "więc"], []);
        task.Items = [ExamItem.Create(task.Id, 1, null, null, [], ["ponieważ"], null)];
        return task;
    }

    public static ExamTask TransformTask(Guid arkuszId, int number = 6)
    {
        var task = ExamTask.Create(arkuszId, number, ExamTaskType.Transform, "Proszę przekształcić zdanie.", 1m,
            null, null, null, [], []);
        task.Items =
        [
            ExamItem.Create(task.Id, 1, "Ola dała bratu książkę.", "dostał", [], ["Brat dostał od Oli książkę", "Brat dostał książkę od Oli"], null),
            ExamItem.Create(task.Id, 2, "Nie pamiętam adresu tego hotelu.", "jaki", [], ["Nie pamiętam, jaki jest adres tego hotelu"], null),
        ];
        return task;
    }

    public static Arkusz TwoTaskArkusz()
    {
        var arkusz = Arkusz.Create("2022-03", "marzec 2022", ArkuszSource.Official, []);
        var part = ExamPart.Create(arkusz.Id, PartKind.Gramatyka, 45, []);
        part.Tasks = [ChooseTask(part.Id), TransformTask(part.Id)];
        arkusz.Parts = [part];
        return arkusz;
    }

    public static ExamPart GramPart(Arkusz arkusz) => arkusz.Parts[0];

    /// <summary>A Pisanie part with two zestawy, each with a short form (a) and a longer form (b); invented prompts.</summary>
    public static ExamPart PisaniePart(Guid arkuszId)
    {
        var part = ExamPart.Create(arkuszId, PartKind.Pisanie, 70, []);
        part.Tasks =
        [
            PisanieTask(part.Id, 1,
                ("Proszę napisać ogłoszenie: sprzedaje Pani/Pan rower, bo przeprowadza się Pani/Pan do innego miasta.", "ogloszenie", 40),
                ("Proszę napisać list do przyjaciela o pierwszym tygodniu w nowej pracy.", "list", 150)),
            PisanieTask(part.Id, 2,
                ("Proszę napisać zaproszenie dla sąsiadów na piknik na podwórku.", "zaproszenie", 40),
                ("Proszę napisać opinię o kawiarni, którą odwiedziła Pani / odwiedził Pan w weekend.", "opinia", 150)),
        ];
        return part;
    }

    private static ExamTask PisanieTask(Guid partId, int number, params (string Prompt, string Form, int Words)[] items)
    {
        var task = ExamTask.Create(partId, number, ExamTaskType.Writing, "Proszę wybrać jeden zestaw i napisać oba teksty.", 10m, null, null, null, [], []);
        task.Items = items.Select((x, i) => ExamItem.Create(task.Id, i + 1, x.Prompt, x.Form, [], [], null, x.Words)).ToList();
        return task;
    }

    public static ExamAttempt AttemptFor(Arkusz arkusz) => ExamAttempt.Create("student-1", arkusz, arkusz.Parts[0], StartedAt);
}
