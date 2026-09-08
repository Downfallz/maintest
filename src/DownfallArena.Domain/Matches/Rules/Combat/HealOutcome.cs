using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Domain.Matches.Rules.Combat;

public sealed record HealOutcome(CreatureId Target, int Amount) : EffectOutcome(Target);
