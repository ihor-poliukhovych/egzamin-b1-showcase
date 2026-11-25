namespace B1.Domain.Auth;

public enum UserRole
{
    Student = 0,
    Teacher = 1,
}

public class User
{
    protected User() { }

    public User(Guid id, string googleSubject, string email, string displayName, UserRole role, DateTimeOffset createdAt)
    {
        Id = id;
        GoogleSubject = googleSubject;
        Email = email;
        DisplayName = displayName;
        Role = role;
        CreatedAt = createdAt;
    }

    public Guid Id { get; set; }
    public string GoogleSubject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string TargetLevel { get; set; } = "B1";
    public string TargetVariant { get; set; } = "dorosli";

    /// <summary>Interface language: uk, pl or en; an empty string means no choice has been made yet.</summary>
    public string UiLanguage { get; set; } = DefaultUiLanguage;

    public const string DefaultUiLanguage = "";
    public static readonly string[] UiLanguages = ["uk", "ru", "pl", "en"];

    /// <summary>Student goal: the exam date they are preparing for; null means not set.</summary>
    public DateOnly? ExamDate { get; private set; }

    /// <summary>A date in the past is not accepted (the goal is always ahead); null clears the goal.</summary>
    public bool SetExamDate(DateOnly? date, DateOnly today)
    {
        if (date is { } d && d < today) return false;
        ExamDate = date;
        return true;
    }

    /// <summary>When the user chose the role themselves (or the Auth:TeacherEmails config assigned it); null means not asked yet.</summary>
    public DateTimeOffset? RoleChosenAt { get; set; }

    public bool RoleChosen => RoleChosenAt is not null;

    public string UserKey => Id.ToString();

    /// <summary>The role is chosen once; a repeated choice is rejected so a teacher does not lose groups by accident.</summary>
    public bool ChooseRole(UserRole role, DateTimeOffset now)
    {
        if (RoleChosen) return false;
        Role = role;
        RoleChosenAt = now;
        return true;
    }

    /// <summary>
    /// Changing the role from the profile is a deliberate action. A teacher's groups are not deleted: while the role is "student",
    /// the groups section is hidden, and when the user switches back to "teacher" everything is still there.
    /// </summary>
    public void ChangeRole(UserRole role, DateTimeOffset now)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        Role = role;
        RoleChosenAt ??= now;
    }

    public static User Create(string googleSubject, string email, string displayName, UserRole role, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(googleSubject)) throw new ArgumentException("googleSubject is required", nameof(googleSubject));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("email is required", nameof(email));
        return new User(Guid.NewGuid(), googleSubject, email.Trim(), string.IsNullOrWhiteSpace(displayName) ? email.Trim() : displayName.Trim(), role, createdAt);
    }

    public static UserRole RoleFor(string email, IEnumerable<string> teacherEmails) =>
        teacherEmails.Any(t => string.Equals(t.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)) ? UserRole.Teacher : UserRole.Student;
}
