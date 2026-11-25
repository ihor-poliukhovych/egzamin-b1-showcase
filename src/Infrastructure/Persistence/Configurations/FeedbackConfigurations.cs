using B1.Domain.Feedback;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B1.Infrastructure.Persistence.Configurations;

public class FeedbackMessageConfiguration : IEntityTypeConfiguration<FeedbackMessage>
{
    public void Configure(EntityTypeBuilder<FeedbackMessage> b)
    {
        b.ToTable("feedback_messages");
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).ValueGeneratedNever();
        b.Property(f => f.UserId).HasMaxLength(128);
        b.Property(f => f.ReplyEmail).HasMaxLength(FeedbackMessage.MaxEmailLength);
        b.Property(f => f.Topic).HasConversion<string>().HasMaxLength(16);
        b.Property(f => f.Message).HasMaxLength(FeedbackMessage.MaxMessageLength).IsRequired();
        b.Property(f => f.Language).HasMaxLength(8);
        b.Property(f => f.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(f => f.ReviewNote).HasMaxLength(FeedbackMessage.MaxNoteLength);
        b.HasIndex(f => new { f.Status, f.CreatedAt });
        b.HasIndex(f => f.UserId);
        b.HasMany<FeedbackAttachment>().WithOne().HasForeignKey(a => a.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FeedbackAttachmentConfiguration : IEntityTypeConfiguration<FeedbackAttachment>
{
    public void Configure(EntityTypeBuilder<FeedbackAttachment> b)
    {
        b.ToTable("feedback_attachments");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.ContentType).HasMaxLength(32).IsRequired();
        b.Property(a => a.Data).IsRequired();
        b.HasIndex(a => a.MessageId);
    }
}
