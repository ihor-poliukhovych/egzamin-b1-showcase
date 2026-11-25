namespace B1.Domain.Feedback;

public enum FeedbackTopic { Question, Bug, Content, Idea, Data, Other }

public enum FeedbackStatus { New, Reviewed }

/// <summary>
/// A message from the feedback form on the "Contact" page. Stored in the database, handled by an agent via the <c>feedback</c> command;
/// after handling it is marked <see cref="FeedbackStatus.Reviewed"/> with a short note.
/// </summary>
public class FeedbackMessage
{
    public const int MinMessageLength = 5;
    public const int MaxMessageLength = 4000;
    public const int MaxEmailLength = 256;
    public const int MaxNoteLength = 1000;

    /// <summary>How many months messages are kept (as stated in the privacy policy).</summary>
    public const int RetentionMonths = 12;

    /// <summary>No more than this many messages per hour from one user.</summary>
    public const int UserHourlyLimit = 5;

    /// <summary>No more than this many messages per hour from all signed-out users combined: protects the database from spam.</summary>
    public const int AnonymousHourlyLimit = 30;

    protected FeedbackMessage() { }

    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? UserId { get; set; }
    public string? ReplyEmail { get; set; }
    public FeedbackTopic Topic { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Language { get; set; }
    public FeedbackStatus Status { get; set; }
    public string? ReviewNote { get; set; }

    public static FeedbackMessage Create(string? userId, FeedbackTopic topic, string message, string? replyEmail, string? language, DateTimeOffset now)
    {
        var text = (message ?? string.Empty).Trim();
        if (text.Length < MinMessageLength) throw new ArgumentException("Message is too short", nameof(message));
        if (text.Length > MaxMessageLength) throw new ArgumentException("Message is too long", nameof(message));

        var email = string.IsNullOrWhiteSpace(replyEmail) ? null : replyEmail.Trim();
        if (email is not null && (email.Length > MaxEmailLength || !IsEmail(email))) throw new ArgumentException("Email is invalid", nameof(replyEmail));

        return new FeedbackMessage
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            ReplyEmail = email,
            Topic = topic,
            Message = text,
            Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant()[..Math.Min(8, language.Trim().Length)],
            Status = FeedbackStatus.New,
        };
    }

    public void MarkReviewed(string? note)
    {
        Status = FeedbackStatus.Reviewed;
        var text = note?.Trim();
        ReviewNote = string.IsNullOrEmpty(text) ? null : text.Length > MaxNoteLength ? text[..MaxNoteLength] : text;
    }

    public static bool LimitReached(bool anonymous, int sentLastHour) =>
        sentLastHour >= (anonymous ? AnonymousHourlyLimit : UserHourlyLimit);

    private static bool IsEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 && at == email.LastIndexOf('@') && email.IndexOf('.', at) > at + 1 && !email.EndsWith('.') && !email.Any(char.IsWhiteSpace);
    }
}
