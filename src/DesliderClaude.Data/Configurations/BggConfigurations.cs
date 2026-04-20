using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesliderClaude.Data.Configurations;

internal sealed class BggGameConfiguration : IEntityTypeConfiguration<BggGame>
{
    public void Configure(EntityTypeBuilder<BggGame> b)
    {
        b.HasKey(g => g.BggGameId);
        b.Property(g => g.BggGameId).ValueGeneratedNever();
        b.Property(g => g.Name).HasMaxLength(256).IsRequired();
        b.Property(g => g.ImageUrl).HasMaxLength(512);
        b.Property(g => g.ThumbnailUrl).HasMaxLength(512);
        b.Property(g => g.LastFetchedAt).IsRequired();
    }
}

internal sealed class BggImportConfiguration : IEntityTypeConfiguration<BggImport>
{
    public void Configure(EntityTypeBuilder<BggImport> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.SourceRef).HasMaxLength(128).IsRequired();
        b.Property(i => i.Name).HasMaxLength(256).IsRequired();
        b.Property(i => i.SourceType).HasConversion<int>();
        b.Property(i => i.CreatedAt).IsRequired();
        b.Property(i => i.LastRefreshedAt).IsRequired();

        b.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One import per (user, source type, source ref) — stops duplicates.
        b.HasIndex(i => new { i.UserId, i.SourceType, i.SourceRef }).IsUnique();
    }
}

internal sealed class BggImportItemConfiguration : IEntityTypeConfiguration<BggImportItem>
{
    public void Configure(EntityTypeBuilder<BggImportItem> b)
    {
        b.HasKey(x => new { x.BggImportId, x.BggGameId });

        b.HasOne(x => x.Import)
            .WithMany(i => i.Items)
            .HasForeignKey(x => x.BggImportId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Game)
            .WithMany(g => g.ImportItems)
            .HasForeignKey(x => x.BggGameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
