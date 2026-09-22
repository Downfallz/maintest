using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rounds;

/// <summary>
/// A player's decision to buy one package for one of their creatures during the Evolution sub-phase: the
/// package's whole spell list and its initiative bonus, for one pick (ADR 0056).
/// </summary>
public sealed record EvolutionChoice(CreatureId Creature, TierId Tier);
