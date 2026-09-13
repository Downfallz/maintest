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
public abstract record EffectOutcome(CreatureId Target)
{
    /// <summary>
    /// Whether this landed on the actor as a caster effect (ADR 0031) rather than on a target of the cast.
    /// <para>
    /// The target alone cannot say: a spell whose targeting origin is <c>Self</c> puts ordinary outcomes on
    /// the actor too, and those are the cast doing what it is for. Anything reading a cast as what it did to
    /// the other side -- the damage an evaluation attributes to a spell, and `tierDamageSpread` behind it --
    /// has to leave these out, or a spell that costs its caster two health reads as hitting two harder.
    /// </para>
    /// </summary>
    public bool OnCaster { get; init; }
}
