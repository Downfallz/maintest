using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>The request and actual result of one living creature's ordinary round-energy grant.</summary>
public sealed record RoundEnergyGrant(CreatureId Creature, int BaseRequested, int PassiveRequested, int Gained, int Before, int After);
