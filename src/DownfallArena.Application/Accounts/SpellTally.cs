namespace DownfallArena.Application.Accounts;

/// <summary>A spell a player's creatures cast: how many times in all, in how many games, and how many of those were won.</summary>
public sealed record SpellTally(string Spell, int Casts, int Games, int Won);
