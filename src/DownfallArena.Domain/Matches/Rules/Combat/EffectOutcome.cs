using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// One consequence of a resolved action on one target. The set of outcomes is closed, like the effects they
/// come from (ADR 0012).
/// <para>
/// The same shapes say two things, and which one is settled by where the outcome came from rather than by its
/// type: a <see cref="CombatResolution.Outcomes"/> is what the rules computed, before the creatures were
/// touched; a <c>CombatActionResolved.AppliedOutcomes</c> is what the board took, which can be smaller.
/// </para>
/// </summary>
public abstract record EffectOutcome(CreatureId Target);
