namespace B1.Domain.Exam;

public enum ArkuszSource
{
    None = 0,
    Official = 1,
    Own = 2,
}

public enum ExamTaskType
{
    None = 0,
    ChooseInText = 1,
    WordBank = 2,
    FillForm = 3,
    AskQuestion = 4,
    Transform = 5,
    MultipleChoice = 6,
    TrueFalse = 7,
    Matching = 8,
    Writing = 9,
    Speaking = 10,
    ShortAnswer = 11,
}

public enum PartKind
{
    None = 0,
    Sluchanie = 1,
    Czytanie = 2,
    Gramatyka = 3,
    Pisanie = 4,
    Mowienie = 5,
}

public enum AnswerVerdict
{
    None = 0,
    Correct = 1,
    Wrong = 2,
    NeedsReview = 3,
    Accepted = 4,
}
