using DesliderClaude.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DesliderClaude.Data;

public sealed class DesliderClaudeDbContext : DbContext
{
    public DesliderClaudeDbContext(DbContextOptions<DesliderClaudeDbContext> options)
        : base(options) { }

    public DbSet<GameNight> GameNights => Set<GameNight>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Voter> Voters => Set<Voter>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<Visitor> Visitors => Set<Visitor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DesliderClaudeDbContext).Assembly);
    }
}
