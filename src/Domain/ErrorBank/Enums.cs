namespace B1.Domain.ErrorBank;

/// <summary>Relapse status: watch = 1 fall, active = 2+ falls, archived = 3 clean runs in a row.</summary>
public enum PatternStatus
{
    Watch = 0,
    Active = 1,
    Archived = 2,
}

public enum PatternCategory
{
    Ortografia = 0,
    Gramatyka = 1,
    Leksyka = 2,
    Rekcja = 3,
    Skladnia = 4,
    Interpunkcja = 5,
}

public enum AttemptResult
{
    Pass = 0,
    Fail = 1,
}

public enum AttemptSource
{
    Blitz = 0,
    Task = 1,
    Pisanie = 2,
    Mowienie = 3,
}
