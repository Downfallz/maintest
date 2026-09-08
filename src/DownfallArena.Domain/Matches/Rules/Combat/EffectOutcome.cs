using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// One computed consequence of a resolved action on one target, before it is applied to the creatures.
/// The set of outcomes is closed, like the effects they come from (ADR 0012).
/// </summary>
public abstract record EffectOutcome(CreatureId Target);
