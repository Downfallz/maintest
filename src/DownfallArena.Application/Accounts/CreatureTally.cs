namespace DownfallArena.Application.Accounts;

/// <summary>A creature a player fielded: how many times, in how many won games, and how often still standing at the end.</summary>
public sealed record CreatureTally(string Creature, int Fielded, int Won, int Survived);
