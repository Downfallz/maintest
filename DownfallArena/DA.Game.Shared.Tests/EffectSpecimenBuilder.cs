using AutoFixture;
using AutoFixture.Kernel;
using DA.Game.Shared.Contracts.Resources.Spells;
using DA.Game.Shared.Contracts.Resources.Spells.Effects;
using DA.Game.Shared.Contracts.Resources.Spells.Enums;

namespace DA.Game.Shared.Tests;

internal sealed class EffectSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type t && t == typeof(IEffect))
        {
            // 0 = Bleed, 1 = Damage
            var choice = Math.Abs(context.Create<int>()) % 2;

            return choice == 0
                ? CreateBleed(context)
                : CreateDamage(context);
        }

        return new NoSpecimen();
    }

    private static Bleed CreateBleed(ISpecimenContext context)
    {
        // amountPerTick ∈ [1, 5]
        var amountPerTick = Math.Abs(context.Create<int>()) % 5 + 1;

        // durationRounds ∈ [1, 4]
        var durationRounds = Math.Abs(context.Create<int>()) % 4 + 1;

        var targeting = CreateRandomTargeting(context);

        return Bleed.Of(amountPerTick, durationRounds, targeting);
    }

    private static Damage CreateDamage(ISpecimenContext context)
    {
        // amount ∈ [1, 10]
        var amount = Math.Abs(context.Create<int>()) % 10 + 1;

        var targeting = CreateRandomTargeting(context);

        return Damage.Of(amount, targeting);
    }

    private static TargetingSpec CreateRandomTargeting(ISpecimenContext context)
    {
        // AutoFixture sait fabriquer des enums => parfait pour randomiser
        var origin = context.Create<TargetOrigin>();
        var scope = context.Create<TargetScope>();

        // maxTargets : null ou 1–3
        int? maxTargets = null;
        var useLimitedTargets = context.Create<bool>();
        if (useLimitedTargets)
        {
            maxTargets = Math.Abs(context.Create<int>()) % 3 + 1;
        }

        return TargetingSpec.Of(origin, scope, maxTargets);
    }
}


