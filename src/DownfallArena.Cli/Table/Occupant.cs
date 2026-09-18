using DownfallArena.Application.Agents;

namespace DownfallArena.Cli.Table;

/// <summary>
/// Whoever is sitting in a seat, and the name a record calls them by (<c>Greedy</c>, <c>human:mk</c>).
/// </summary>
/// <remarks>
/// The name travels with the agent rather than beside it, because a seat changes hands while a match runs and
/// the two must change together: a swap that moved the agent and left the name behind would write the old
/// name on the new player's decisions, which is the one thing <c>StepRecord.DecidedBy</c> exists to prevent.
/// </remarks>
internal sealed record Occupant(IPlayerAgent Agent, string Name);
