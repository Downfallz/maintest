using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>What one creature's regeneration conditions healed at the start of a round.</summary>
public sealed record RegenerationTick(CreatureId Creature, int Healed);
