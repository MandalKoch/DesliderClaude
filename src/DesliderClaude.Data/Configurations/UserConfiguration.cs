using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesliderClaude.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Username).HasMaxLength(64).IsRequired();
        b.HasIndex(u => u.Username).IsUnique();
        b.Property(u => u.PasswordHash).HasMaxLength(256);
        b.Property(u => u.CreatedAt).IsRequired();

        b.HasMany(u => u.ExternalLogins)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Provider).HasMaxLength(32).IsRequired();
        b.Property(l => l.ProviderUserId).HasMaxLength(128).IsRequired();
        b.HasIndex(l => new { l.Provider, l.ProviderUserId }).IsUnique();
    }
}
