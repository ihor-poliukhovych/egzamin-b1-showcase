using System.Text.Json;
using B1.Domain.Exam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B1.Infrastructure.Persistence.Configurations;

internal static class ExamTables
{
    public const string Arkusze = "arkusze";
    public const string Parts = "exam_parts";
    public const string Tasks = "exam_tasks";
    public const string Items = "exam_items";
    public const string Sessions = "exam_sessions";
    public const string Attempts = "exam_attempts";
    public const string Answers = "exam_answers";
}

internal static class StringListConversion
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly ValueComparer<List<string>> Comparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
        v => v.ToList());

    public static PropertyBuilder<List<string>> AsJson(this PropertyBuilder<List<string>> builder) => builder
        .HasConversion(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>(),
            Comparer);
}

public class ArkuszConfiguration : IEntityTypeConfiguration<Arkusz>
{
    public void Configure(EntityTypeBuilder<Arkusz> b)
    {
        b.ToTable(ExamTables.Arkusze);
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Code).HasMaxLength(32).IsRequired();
        b.Property(a => a.Title).HasMaxLength(128).IsRequired();
        b.Property(a => a.Source).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(a => a.Code).IsUnique();
        b.Ignore(a => a.MaxPoints);

        b.HasMany(a => a.Parts)
            .WithOne()
            .HasForeignKey(p => p.ArkuszId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExamPartConfiguration : IEntityTypeConfiguration<ExamPart>
{
    public void Configure(EntityTypeBuilder<ExamPart> b)
    {
        b.ToTable(ExamTables.Parts);
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Kind).HasConversion<string>().HasMaxLength(16);
        b.Ignore(p => p.MaxPoints);
        b.HasIndex(p => new { p.ArkuszId, p.Kind }).IsUnique();

        b.HasMany(p => p.Tasks)
            .WithOne()
            .HasForeignKey(t => t.PartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExamTaskConfiguration : IEntityTypeConfiguration<ExamTask>
{
    public void Configure(EntityTypeBuilder<ExamTask> b)
    {
        b.ToTable(ExamTables.Tasks);
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(t => t.Instruction).IsRequired();
        b.Property(t => t.PointsPerItem).HasPrecision(5, 2);
        b.Property(t => t.WordBank).AsJson();
        b.Ignore(t => t.MaxPoints);
        b.Ignore(t => t.RomanNumber);
        b.Ignore(t => t.IsFreeText);
        b.HasIndex(t => new { t.PartId, t.Number }).IsUnique();

        b.HasMany(t => t.Items)
            .WithOne()
            .HasForeignKey(i => i.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExamItemConfiguration : IEntityTypeConfiguration<ExamItem>
{
    public void Configure(EntityTypeBuilder<ExamItem> b)
    {
        b.ToTable(ExamTables.Items);
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.PointsOverride).HasPrecision(5, 2);
        b.Property(i => i.Options).AsJson();
        b.Property(i => i.AnswerKey).AsJson();
        b.HasIndex(i => new { i.TaskId, i.Number }).IsUnique();
    }
}

public class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> b)
    {
        b.ToTable(ExamTables.Sessions);
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.UserId).HasMaxLength(128).IsRequired();
        b.HasIndex(s => new { s.UserId, s.ArkuszId });
        b.HasOne<Arkusz>().WithMany().HasForeignKey(s => s.ArkuszId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> b)
    {
        b.ToTable(ExamTables.Attempts);
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.UserId).HasMaxLength(128).IsRequired();
        b.Property(a => a.PartKind).HasConversion<string>().HasMaxLength(16);
        b.Property(a => a.Points).HasPrecision(6, 2);
        b.Property(a => a.MaxPoints).HasPrecision(6, 2);
        b.Ignore(a => a.IsSubmitted);
        b.HasIndex(a => new { a.UserId, a.ArkuszId });
        b.HasIndex(a => a.SessionId);

        b.HasOne<Arkusz>()
            .WithMany()
            .HasForeignKey(a => a.ArkuszId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(a => a.Answers)
            .WithOne()
            .HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExamAnswerConfiguration : IEntityTypeConfiguration<ExamAnswer>
{
    public void Configure(EntityTypeBuilder<ExamAnswer> b)
    {
        b.ToTable(ExamTables.Answers);
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Value).IsRequired();
        b.Property(a => a.Verdict).HasConversion<string>().HasMaxLength(16);
        b.Property(a => a.Points).HasPrecision(5, 2);
        b.HasIndex(a => new { a.AttemptId, a.ItemId }).IsUnique();
    }
}

public class AiReviewUsageConfiguration : IEntityTypeConfiguration<AiReviewUsage>
{
    public void Configure(EntityTypeBuilder<AiReviewUsage> b)
    {
        b.ToTable("ai_review_usages");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.UserId).HasMaxLength(128).IsRequired();
        b.Property(u => u.Part).HasConversion<string>().HasMaxLength(16);
        b.Property(u => u.Kind).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(u => new { u.UserId, u.At });
        b.HasIndex(u => new { u.UserId, u.Kind, u.At });
    }
}
