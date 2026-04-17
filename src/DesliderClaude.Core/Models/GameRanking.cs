namespace DesliderClaude.Core.Models;

public sealed record GameRanking(Guid GameId, string Name, int YesCount, int NoCount);
