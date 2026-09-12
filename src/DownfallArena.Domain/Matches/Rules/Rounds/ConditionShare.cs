using DownfallArena.Domain.Matches.Creatures;

namespace DownfallArena.Domain.Matches.Rules.Rounds;

/// <summary>
/// How much of one upkeep tick a single spell's condition is answerable for (ADR 0027).
/// <para>
/// The shares of a tick add up to exactly what the board took, never to what the conditions asked for: a
/// creature with two points of bleed and one point of health takes one, and one is what the shares carry.
/// </para>
/// </summary>
public sealed record ConditionShare(ConditionSource Source, int Amount);
