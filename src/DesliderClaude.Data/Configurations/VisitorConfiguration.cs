using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesliderClaude.Data.Configurations;

internal sealed class VisitorConfiguration : IEntityTypeConfiguration<Visitor>
{
    public void Configure(EntityTypeBuilder<Visitor> b)
    {
        b.HasKey(v => v.Id);
        b.Property(v => v.Token).HasMaxLength(128).IsRequired();
        b.HasIndex(v => v.Token).IsUnique();
        b.Property(v => v.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(v => v.CreatedAt).IsRequired();
        b.Property(v => v.LastSeenAt).IsRequired();
    }
}
