namespace B1.Domain.Groups;

public enum InviteState
{
    Ok = 0,
    Revoked = 1,
    Expired = 2,
    Exhausted = 3,
}

public class Group
{
    protected Group() { }

    public Group(Guid id, string teacherId, string name, DateTimeOffset createdAt)
    {
        Id = id;
        TeacherId = teacherId;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; set; }
    public string TeacherId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    public static Group Create(string teacherId, string name, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(teacherId)) throw new ArgumentException("teacherId is required", nameof(teacherId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required", nameof(name));
        return new Group(Guid.NewGuid(), teacherId, name.Trim(), createdAt);
    }
}

public class GroupMember
{
    protected GroupMember() { }

    public GroupMember(Guid groupId, string studentId, DateTimeOffset joinedAt, DateTimeOffset? consentAt)
    {
        GroupId = groupId;
        StudentId = studentId;
        JoinedAt = joinedAt;
        ConsentAt = consentAt;
    }

    public Guid GroupId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? ConsentAt { get; set; }
}

public class GroupInvite
{
    protected GroupInvite() { }

    public GroupInvite(Guid id, Guid groupId, string token, DateTimeOffset expiresAt, int? maxUses)
    {
        Id = id;
        GroupId = groupId;
        Token = token;
        ExpiresAt = expiresAt;
        MaxUses = maxUses;
    }

    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public int Uses { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public const int MaxLifetimeDays = 365;
    public const int MaxUsesLimit = 1000;

    /// <summary>Validation of a teacher request before creation: duration 1-365 days, limit 1-1000 or no limit.</summary>
    public static Dictionary<string, string[]> Validate(int days, int? maxUses)
    {
        var errors = new Dictionary<string, string[]>();
        if (days is < 1 or > MaxLifetimeDays) errors["days"] = [$"Days must be between 1 and {MaxLifetimeDays}"];
        if (maxUses is < 1 or > MaxUsesLimit) errors["maxUses"] = [$"MaxUses must be between 1 and {MaxUsesLimit}"];
        return errors;
    }

    public static GroupInvite Create(Guid groupId, DateTimeOffset now, TimeSpan lifetime, int? maxUses)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(MaxLifetimeDays)) throw new ArgumentOutOfRangeException(nameof(lifetime));
        if (maxUses is <= 0 or > MaxUsesLimit) throw new ArgumentOutOfRangeException(nameof(maxUses));
        return new GroupInvite(Guid.NewGuid(), groupId, NewToken(), now + lifetime, maxUses);
    }

    /// <summary>128 bits of randomness in base64url (22 characters).</summary>
    public static string NewToken() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public InviteState Evaluate(DateTimeOffset now)
    {
        if (RevokedAt is not null) return InviteState.Revoked;
        if (now >= ExpiresAt) return InviteState.Expired;
        if (MaxUses is not null && Uses >= MaxUses) return InviteState.Exhausted;
        return InviteState.Ok;
    }
}
