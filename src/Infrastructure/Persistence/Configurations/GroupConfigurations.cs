using B1.Domain.Groups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B1.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        b.ToTable("groups");
        b.HasKey(g => g.Id);
        b.Property(g => g.Id).ValueGeneratedNever();
        b.Property(g => g.TeacherId).HasMaxLength(128).IsRequired();
        b.Property(g => g.Name).HasMaxLength(200).IsRequired();
        b.Ignore(g => g.IsArchived);
        b.HasIndex(g => g.TeacherId);
    }
}

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> b)
    {
        b.ToTable("group_members");
        b.HasKey(m => new { m.GroupId, m.StudentId });
        b.Property(m => m.StudentId).HasMaxLength(128).IsRequired();
        b.HasIndex(m => m.StudentId);
        b.HasOne<Group>().WithMany().HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GroupInviteConfiguration : IEntityTypeConfiguration<GroupInvite>
{
    public void Configure(EntityTypeBuilder<GroupInvite> b)
    {
        b.ToTable("group_invites");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.Token).HasMaxLength(64).IsRequired();
        b.HasIndex(i => i.Token).IsUnique();
        b.HasOne<Group>().WithMany().HasForeignKey(i => i.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
