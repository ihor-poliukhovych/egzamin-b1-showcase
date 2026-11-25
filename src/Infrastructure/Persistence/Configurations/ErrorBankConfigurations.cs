using B1.Domain.ErrorBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B1.Infrastructure.Persistence.Configurations;

public class ErrorPatternConfiguration : IEntityTypeConfiguration<ErrorPattern>
{
    public void Configure(EntityTypeBuilder<ErrorPattern> b)
    {
        b.ToTable("error_patterns");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.UserId).HasMaxLength(128).IsRequired();
        b.Property(p => p.Code).HasMaxLength(16);
        b.Property(p => p.Title).HasMaxLength(512).IsRequired();
        b.Property(p => p.WrongExample).IsRequired();
        b.Property(p => p.CorrectForm).IsRequired();
        b.Property(p => p.Rule).IsRequired();
        b.Property(p => p.Category).HasConversion<string>().HasMaxLength(32);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(p => p.GroupName).HasMaxLength(ErrorGrouping.MaxGroupNameLength);

        b.HasIndex(p => new { p.UserId, p.Status });
        b.HasIndex(p => new { p.UserId, p.Code }).IsUnique();

        b.HasMany(p => p.Attempts)
            .WithOne()
            .HasForeignKey(a => a.PatternId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Navigation(p => p.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> b)
    {
        b.ToTable("attempts");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Result).HasConversion<string>().HasMaxLength(8);
        b.Property(a => a.Source).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(a => new { a.PatternId, a.Date });
    }
}

public class ReviewSessionConfiguration : IEntityTypeConfiguration<ReviewSession>
{
    public void Configure(EntityTypeBuilder<ReviewSession> b)
    {
        b.ToTable("review_sessions");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.UserId).HasMaxLength(128).IsRequired();
        b.HasIndex(s => new { s.UserId, s.Date }).IsUnique();

        b.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ReviewItemConfiguration : IEntityTypeConfiguration<ReviewItem>
{
    public void Configure(EntityTypeBuilder<ReviewItem> b)
    {
        b.ToTable("review_items");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.Result).HasConversion<string?>().HasMaxLength(8);
        b.HasIndex(i => new { i.SessionId, i.PatternId }).IsUnique();
    }
}
