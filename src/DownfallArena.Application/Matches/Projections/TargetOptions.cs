using DownfallArena.Domain.Matches.Rules.Combat;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Projections;

/// <summary>
/// The intent being revealed belongs to the player: the actor, the declared spell, and its legal targets. When
/// the legal targets are not castable (no candidate is left), the only valid action binds no target and fizzles.
/// </summary>
public sealed record TargetOptions(CreatureId Actor, SpellId Spell, LegalTargets LegalTargets);
