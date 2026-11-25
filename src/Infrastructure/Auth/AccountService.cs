using B1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B1.Infrastructure.Auth;

/// <summary>Account deletion under GDPR (RODO): the user and all their data, in one transaction.</summary>
public class AccountService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AccountService> _logger;

    public AccountService(AppDbContext db, ILogger<AccountService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <returns>false if there is no such user.</returns>
    public async Task<bool> DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return false;
        var key = user.UserKey;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // exam results: answers -> attempts -> sessions (attempts reference the session)
        await _db.ExamAnswers.Where(a => _db.ExamAttempts.Any(t => t.Id == a.AttemptId && t.UserId == key)).ExecuteDeleteAsync(ct);
        await _db.ExamAttempts.Where(t => t.UserId == key).ExecuteDeleteAsync(ct);
        await _db.ExamSessions.Where(s => s.UserId == key).ExecuteDeleteAsync(ct);
        await _db.AiReviewUsages.Where(u => u.UserId == key).ExecuteDeleteAsync(ct);
        var feedbackIds = await _db.FeedbackMessages.Where(f => f.UserId == key).Select(f => f.Id).ToListAsync(ct);
        await _db.FeedbackAttachments.Where(a => feedbackIds.Contains(a.MessageId)).ExecuteDeleteAsync(ct);
        await _db.FeedbackMessages.Where(f => f.UserId == key).ExecuteDeleteAsync(ct);

        // error bank: blitz items and pattern attempts -> blitz sessions and patterns
        await _db.ReviewItems.Where(i => _db.ReviewSessions.Any(s => s.Id == i.SessionId && s.UserId == key)).ExecuteDeleteAsync(ct);
        await _db.ReviewSessions.Where(s => s.UserId == key).ExecuteDeleteAsync(ct);
        await _db.Attempts.Where(a => _db.ErrorPatterns.Any(p => p.Id == a.PatternId && p.UserId == key)).ExecuteDeleteAsync(ct);
        await _db.ErrorPatterns.Where(p => p.UserId == key).ExecuteDeleteAsync(ct);

        // groups: the student's memberships and the teacher's own groups (with invites and members)
        await _db.GroupMembers.Where(m => m.StudentId == key).ExecuteDeleteAsync(ct);
        await _db.GroupInvites.Where(i => _db.Groups.Any(g => g.Id == i.GroupId && g.TeacherId == key)).ExecuteDeleteAsync(ct);
        await _db.GroupMembers.Where(m => _db.Groups.Any(g => g.Id == m.GroupId && g.TeacherId == key)).ExecuteDeleteAsync(ct);
        await _db.Groups.Where(g => g.TeacherId == key).ExecuteDeleteAsync(ct);

        await _db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Account {UserId} deleted with all data", userId);
        return true;
    }
}
