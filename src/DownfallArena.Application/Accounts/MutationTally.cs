namespace DownfallArena.Application.Accounts;

/// <summary>A mutation a player's creatures bought: how many times, and how many of those in games won.</summary>
public sealed record MutationTally(string Mutation, int Bought, int Won);
