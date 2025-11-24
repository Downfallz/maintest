using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Services.Combat.Execution;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;

namespace DA.Game.Domain2.Matches.Services.Combat;

public sealed class EffectComputationServiceV1 : IEffectComputationService
{
    public RawEffectBundle ComputeRawEffects(
        CombatActionChoice intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (intent.TargetIds is null || intent.TargetIds.Count == 0)
            return RawEffectBundle.Empty;

        var instantApplications = new List<InstantEffectApplication>();
        var conditionApplications = new List<ConditionApplication>();

        foreach (var effect in intent.SpellRef.Effects)
        {
            switch (effect)
            {
                case Damage damage:
                    instantApplications.AddRange(CreateDamageApplications(intent, damage));
                    break;


                case Bleed bleed:
                    conditionApplications.AddRange(CreateBleedConditions(intent, bleed));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Effect type '{effect.GetType().Name}' is not supported by {nameof(EffectComputationServiceV1)}. " +
                        "Make sure all effect types are handled in the computation service.");

            }
        }

        return new RawEffectBundle(
            instantApplications.ToArray(),
            conditionApplications.ToArray());
    }

    private static IEnumerable<InstantEffectApplication> CreateDamageApplications(
        CombatActionChoice intent,
        Damage damage)
    {
        return intent.TargetIds.Select(targetId =>
            new InstantEffectApplication(
                intent.ActorId,
                targetId,
                damage.Kind,
                damage.Amount));
    }

    private static IEnumerable<ConditionApplication> CreateBleedConditions(
        CombatActionChoice intent,
        Bleed bleed)
    {
        return intent.TargetIds.Select(targetId =>
            new ConditionApplication(targetId));
    }
}
