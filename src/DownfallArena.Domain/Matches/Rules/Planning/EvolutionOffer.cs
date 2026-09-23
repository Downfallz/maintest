using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Planning;

/// <summary>
/// A creature a pick can go to this round, with the packages it can buy: living, its owner's, not yet bought
/// this round (ADR 0066), and with something left to buy.
/// </summary>
public sealed record EvolutionOffer(CreatureId Creature, IReadOnlyList<TierId> Tiers);
