using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DesliderClaude.Data.Configurations;

internal sealed class GatheringConfiguration : IEntityTypeConfiguration<Gathering>
{
    public void Configure(EntityTypeBuilder<Gathering> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ShareCode).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.ShareCode).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Rounds).WithOne(x => x.Gathering).HasForeignKey(x => x.GatheringId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class GatheringMemberConfiguration : IEntityTypeConfiguration<GatheringMember>
{
    public void Configure(EntityTypeBuilder<GatheringMember> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Token).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.GatheringId, x.Token }).IsUnique();
        builder.HasIndex(x => new { x.GatheringId, x.UserId }).IsUnique();
        builder.HasOne(x => x.Gathering).WithMany(x => x.Members).HasForeignKey(x => x.GatheringId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class GatheringGameConfiguration : IEntityTypeConfiguration<GatheringGame>
{
    public void Configure(EntityTypeBuilder<GatheringGame> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(2000);
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(2000);
        builder.HasOne(x => x.Gathering).WithMany(x => x.Games).HasForeignKey(x => x.GatheringId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SuggestedByMember).WithMany().HasForeignKey(x => x.SuggestedByMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BringerMember).WithMany().HasForeignKey(x => x.BringerMemberId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany<Game>().WithOne(x => x.GatheringGame).HasForeignKey(x => x.GatheringGameId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class GatheringInterestConfiguration : IEntityTypeConfiguration<GatheringInterest>
{
    public void Configure(EntityTypeBuilder<GatheringInterest> builder)
    {
        builder.HasKey(x => new { x.GatheringGameId, x.MemberId });
        builder.HasOne(x => x.GatheringGame).WithMany(x => x.Interests).HasForeignKey(x => x.GatheringGameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GatheringPlayedGameConfiguration : IEntityTypeConfiguration<GatheringPlayedGame>
{
    public void Configure(EntityTypeBuilder<GatheringPlayedGame> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Gathering).WithMany(x => x.PlayedGames).HasForeignKey(x => x.GatheringId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.GatheringGame).WithMany().HasForeignKey(x => x.GatheringGameId).OnDelete(DeleteBehavior.Cascade);
    }
}
