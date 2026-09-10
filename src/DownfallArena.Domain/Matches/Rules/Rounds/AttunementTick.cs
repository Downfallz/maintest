using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>What one creature's attunement conditions gave it at the start of a round.</summary>
public sealed record AttunementTick(CreatureId Creature, int Gained);
