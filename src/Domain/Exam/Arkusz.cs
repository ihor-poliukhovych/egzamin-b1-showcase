namespace B1.Domain.Exam;

public class Arkusz
{
    protected Arkusz() { }

    public Arkusz(Guid id, string code, string title, ArkuszSource source, List<ExamPart> parts)
    {
        Id = id;
        Code = code;
        Title = title;
        Source = source;
        Parts = parts;
    }

    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ArkuszSource Source { get; set; }
    public string Level { get; set; } = "B1";
    public string Variant { get; set; } = "dorosli";
    public List<ExamPart> Parts { get; set; } = [];

    public decimal MaxPoints => Parts.Sum(p => p.MaxPoints);

    public static readonly PartKind[] CoreParts = [PartKind.Sluchanie, PartKind.Czytanie, PartKind.Gramatyka, PartKind.Pisanie];

    /// <summary>The full "Egzamin próbny" is available only where all main parts exist; individual parts can still be practised on an incomplete arkusz.</summary>
    public bool IsFullExam => CoreParts.All(k => Parts.Any(p => p.Kind == k));

    public static Arkusz Create(string code, string title, ArkuszSource source, List<ExamPart> parts, string level = "B1", string variant = "dorosli")
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code is required", nameof(code));
        var arkusz = new Arkusz(Guid.NewGuid(), code, title, source, parts);
        arkusz.Level = level;
        arkusz.Variant = variant;
        return arkusz;
    }

    public ExamPart? GetPart(PartKind kind) => Parts.FirstOrDefault(p => p.Kind == kind);
}
