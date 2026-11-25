using B1.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B1.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.GoogleSubject).HasMaxLength(128).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
        b.Property(u => u.Role).HasConversion<int>();
        b.Ignore(u => u.UserKey);
        b.HasIndex(u => u.GoogleSubject).IsUnique();
        b.HasIndex(u => u.Email);
    }
}
