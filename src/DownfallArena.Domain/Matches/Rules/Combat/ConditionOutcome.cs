using DownfallArena.Domain.Resources.Effects;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

/// <summary>
/// A lasting effect to attach to the target as a condition, per its stacking policy.
/// </summary>
public sealed record ConditionOutcome(CreatureId Target, LastingEffect Effect) : EffectOutcome(Target);
