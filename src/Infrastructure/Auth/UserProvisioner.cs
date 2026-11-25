using B1.Domain.Auth;
using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace B1.Infrastructure.Auth;

public class AuthSettings
{
    /// <summary>Emails that get the Teacher role on first sign-in (Auth:TeacherEmails).</summary>
    public string[] TeacherEmails { get; set; } = [];

    public static AuthSettings From(IConfiguration configuration) => new()
    {
        TeacherEmails = configuration.GetSection("Auth:TeacherEmails").Get<string[]>() ?? [],
    };
}

public class UserProvisioner
{
    private readonly AppDbContext _appDbContext;
    private readonly AuthSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserProvisioner> _logger;

    public UserProvisioner(AppDbContext appDbContext, AuthSettings settings, TimeProvider timeProvider, ILogger<UserProvisioner> logger)
    {
        _appDbContext = appDbContext;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<User> FindOrCreateAsync(string googleSubject, string email, string displayName, CancellationToken ct = default)
    {
        var role = User.RoleFor(email, _settings.TeacherEmails);
        var user = await _appDbContext.Users.FirstOrDefaultAsync(u => u.GoogleSubject == googleSubject, ct);

        if (user is null)
        {
            user = User.Create(googleSubject, email, displayName, role, _timeProvider.GetUtcNow());
            // a teacher from the configured list is not asked; everyone else picks a role on the first screen
            if (role == UserRole.Teacher) user.RoleChosenAt = user.CreatedAt;
            _appDbContext.Users.Add(user);
            _logger.LogInformation("UserProvisioner: created user {UserId} role={Role}", user.Id, user.Role);
        }
        else
        {
            user.Email = email.Trim();
            if (!string.IsNullOrWhiteSpace(displayName)) user.DisplayName = displayName.Trim();
            // the configured teacher list only decides for users who have not chosen a role yet; a choice made in the profile wins
            if (role == UserRole.Teacher && user.RoleChosenAt is null)
            {
                user.Role = UserRole.Teacher;
                user.RoleChosenAt = _timeProvider.GetUtcNow();
            }
        }

        await _appDbContext.SaveChangesAsync(ct);
        return user;
    }
}
