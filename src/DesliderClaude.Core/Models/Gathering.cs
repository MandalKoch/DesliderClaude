namespace DesliderClaude.Core.Models;

public class Gathering
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public string ShareCode { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<GatheringMember> Members { get; set; } = new List<GatheringMember>();
    public ICollection<GatheringGame> Games { get; set; } = new List<GatheringGame>();
    public ICollection<GameNight> Rounds { get; set; } = new List<GameNight>();
    public ICollection<GatheringPlayedGame> PlayedGames { get; set; } = new List<GatheringPlayedGame>();
}

public class GatheringMember
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GatheringId { get; set; }
    public Gathering Gathering { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
}

public class GatheringGame
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GatheringId { get; set; }
    public Gathering Gathering { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int? BggGameId { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public Guid SuggestedByMemberId { get; set; }
    public GatheringMember SuggestedByMember { get; set; } = null!;
    public bool IsConfirmed { get; set; }
    public Guid? BringerMemberId { get; set; }
    public GatheringMember? BringerMember { get; set; }
    public ICollection<GatheringInterest> Interests { get; set; } = new List<GatheringInterest>();
}

public class GatheringInterest
{
    public Guid GatheringGameId { get; set; }
    public GatheringGame GatheringGame { get; set; } = null!;
    public Guid MemberId { get; set; }
    public GatheringMember Member { get; set; } = null!;
    public bool Interested { get; set; }
}

public class GatheringPlayedGame
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GatheringId { get; set; }
    public Gathering Gathering { get; set; } = null!;
    public Guid GatheringGameId { get; set; }
    public GatheringGame GatheringGame { get; set; } = null!;
    public DateTimeOffset PlayedAt { get; set; } = DateTimeOffset.UtcNow;
}
