using B1.Domain.Auth;
using B1.Domain.ErrorBank;
using B1.Domain.Exam;
using B1.Domain.Feedback;
using B1.Domain.Groups;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace B1.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    // Sign-in cookie encryption keys: in the cloud the container sleeps and is recreated; without shared storage everyone would be signed out.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupInvite> GroupInvites => Set<GroupInvite>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ErrorPattern> ErrorPatterns => Set<ErrorPattern>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<ReviewItem> ReviewItems => Set<ReviewItem>();

    public DbSet<Arkusz> Arkusze => Set<Arkusz>();
    public DbSet<ExamPart> ExamParts => Set<ExamPart>();
    public DbSet<ExamTask> ExamTasks => Set<ExamTask>();
    public DbSet<ExamItem> ExamItems => Set<ExamItem>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAnswer> ExamAnswers => Set<ExamAnswer>();
    public DbSet<AiReviewUsage> AiReviewUsages => Set<AiReviewUsage>();
    public DbSet<FeedbackMessage> FeedbackMessages => Set<FeedbackMessage>();
    public DbSet<FeedbackAttachment> FeedbackAttachments => Set<FeedbackAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
