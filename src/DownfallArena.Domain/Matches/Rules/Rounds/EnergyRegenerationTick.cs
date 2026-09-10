using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>What one creature's energyRegeneration conditions gave it at the start of a round.</summary>
public sealed record EnergyRegenerationTick(CreatureId Creature, int Gained);
